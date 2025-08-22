# Concurrency and columnar databases

Columnar databases present unique challenges for concurrency control compared to traditional relational databases. Unlike OLTP systems, columnar databases often don't enforce primary key uniqueness constraints, and bulk operations are the norm rather than single-row transactions. This section covers best practices for handling concurrent data operations in columnar databases.

## Primary Key Uniqueness Challenges

### The Problem

Most columnar databases (like Snowflake, BigQuery, and others) do not enforce primary key uniqueness constraints during data insertion. This creates several challenges:

1. **Duplicate Records**: Retry operations or replayed data loads can create duplicate rows
2. **Insert Failures**: If uniqueness is checked, retries cause insert operations to fail
3. **Data Inconsistency**: Without proper handling, newer data might be overwritten by older data

### Real-World Scenarios

```sql
-- Example: Direct insert that can cause problems
INSERT INTO products (product_id, name, price, data_timestamp)
VALUES 
    ('prod-001', 'Widget A', 10.99, '2025-01-15 14:30:00'),
    ('prod-002', 'Widget B', 15.99, '2025-01-15 14:30:00');

-- If this operation is retried due to network failure:
-- 1. Snowflake: Creates duplicate rows (no uniqueness enforcement)
-- 2. Systems with checks: Operation fails with constraint violation
```

## Best Practice: Staging Table Pattern with Temporary Naming

The recommended approach uses regular tables with temporary naming conventions to achieve idempotent, safe bulk operations regardless of retries or failures.

### Important Distinction: Not Actual Temporary Tables

**These are NOT database temporary tables** (which are session-specific). Instead, these are **regular tables with naming conventions** that indicate temporary usage. This distinction is crucial because:

- **Actual temporary tables** are session-specific and would fail with distributed writes (e.g., from Spark clusters)
- **Staging tables with temporary names** are regular tables that can be accessed by multiple sessions/processes
- **Distributed processing frameworks** like Spark, Databricks, BigQuery jobs can all write to these staging tables

### Pattern Overview

1. **Create staging table** with unique timestamp-based suffix (e.g., `_temp_20250107_143000`)
2. **Insert data** into staging table (supports distributed writes)
3. **Merge data** from staging table to target table
4. **Drop staging table** after successful merge

This pattern ensures:
- No duplicates regardless of retries
- Atomic operation (all or nothing)
- Ability to handle both inserts and updates
- Protection against overwriting newer data with older data
- **Compatible with distributed processing** (Spark, Databricks clusters, BigQuery jobs)

## Implementation Examples

### Databricks Delta

```sql
-- Step 1: Create temporary table with timestamp suffix
CREATE OR REPLACE TABLE products_temp_20250107_143000 (
    product_id STRING NOT NULL,
    name STRING NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    data_timestamp TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP(),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP()
) USING DELTA;

-- Step 2: Insert new data into temporary table
INSERT INTO products_temp_20250107_143000 
(product_id, name, price, data_timestamp)
VALUES 
    ('prod-001', 'Updated Widget A', 12.99, '2025-01-07 14:30:00'),
    ('prod-002', 'Widget B', 15.99, '2025-01-07 14:30:00'),
    ('prod-003', 'New Widget C', 8.99, '2025-01-07 14:30:00');

-- Step 3: Merge from temporary table to main table
MERGE INTO products AS target
USING products_temp_20250107_143000 AS source
ON target.product_id = source.product_id
WHEN MATCHED AND source.data_timestamp >= target.data_timestamp THEN
    UPDATE SET 
        name = source.name,
        price = source.price,
        data_timestamp = source.data_timestamp,
        updated_at = CURRENT_TIMESTAMP()
WHEN NOT MATCHED THEN
    INSERT (product_id, name, price, data_timestamp, created_at, updated_at)
    VALUES (source.product_id, source.name, source.price, source.data_timestamp, 
            CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP());

-- Step 4: Drop temporary table
DROP TABLE products_temp_20250107_143000;
```

### Advanced Delta Example with Conflict Resolution

```sql
-- More sophisticated merge with detailed logging
CREATE OR REPLACE TABLE products_temp_20250107_143000 (
    product_id STRING NOT NULL,
    name STRING NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    data_timestamp TIMESTAMP NOT NULL,
    batch_id STRING DEFAULT 'batch_20250107_143000'
) USING DELTA;

INSERT INTO products_temp_20250107_143000 
(product_id, name, price, data_timestamp)
VALUES 
    ('prod-001', 'Updated Widget A', 12.99, '2025-01-07 14:30:00'),
    ('prod-002', 'Widget B', 15.99, '2025-01-07 14:30:00'),
    ('prod-003', 'New Widget C', 8.99, '2025-01-07 14:30:00');

-- Merge with comprehensive conflict resolution
MERGE INTO products AS target
USING products_temp_20250107_143000 AS source
ON target.product_id = source.product_id
WHEN MATCHED AND source.data_timestamp > target.data_timestamp THEN
    UPDATE SET 
        name = source.name,
        price = source.price,
        data_timestamp = source.data_timestamp,
        updated_at = CURRENT_TIMESTAMP(),
        version = target.version + 1
WHEN MATCHED AND source.data_timestamp = target.data_timestamp THEN
    UPDATE SET 
        updated_at = CURRENT_TIMESTAMP() -- Touch record but don't change data
WHEN NOT MATCHED THEN
    INSERT (product_id, name, price, data_timestamp, created_at, updated_at, version)
    VALUES (source.product_id, source.name, source.price, source.data_timestamp, 
            CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP(), 1);

DROP TABLE products_temp_20250107_143000;
```

### GCP BigQuery

```sql
-- Step 1: Create temporary table
CREATE OR REPLACE TABLE `project.dataset.products_temp_20250107_143000` (
    product_id STRING NOT NULL,
    name STRING NOT NULL,
    price NUMERIC(10,2) NOT NULL,
    data_timestamp TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP(),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP()
);

-- Step 2: Insert data into temporary table
INSERT INTO `project.dataset.products_temp_20250107_143000` 
(product_id, name, price, data_timestamp)
VALUES 
    ('prod-001', 'Updated Widget A', 12.99, TIMESTAMP('2025-01-07 14:30:00')),
    ('prod-002', 'Widget B', 15.99, TIMESTAMP('2025-01-07 14:30:00')),
    ('prod-003', 'New Widget C', 8.99, TIMESTAMP('2025-01-07 14:30:00'));

-- Step 3: Merge using BigQuery's MERGE statement
MERGE `project.dataset.products` AS target
USING `project.dataset.products_temp_20250107_143000` AS source
ON target.product_id = source.product_id
WHEN MATCHED AND source.data_timestamp >= target.data_timestamp THEN
    UPDATE SET 
        name = source.name,
        price = source.price,
        data_timestamp = source.data_timestamp,
        updated_at = CURRENT_TIMESTAMP()
WHEN NOT MATCHED THEN
    INSERT (product_id, name, price, data_timestamp, created_at, updated_at)
    VALUES (source.product_id, source.name, source.price, source.data_timestamp, 
            CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP());

-- Step 4: Drop temporary table
DROP TABLE `project.dataset.products_temp_20250107_143000`;
```

### BigQuery with Change Data Capture Pattern

```sql
-- Create temp table with operation tracking
CREATE OR REPLACE TABLE `project.dataset.products_temp_20250107_143000` (
    product_id STRING NOT NULL,
    name STRING,
    price NUMERIC(10,2),
    data_timestamp TIMESTAMP NOT NULL,
    operation_type STRING DEFAULT 'UPSERT', -- INSERT, UPDATE, DELETE
    batch_id STRING DEFAULT 'batch_20250107_143000'
);

INSERT INTO `project.dataset.products_temp_20250107_143000` 
(product_id, name, price, data_timestamp, operation_type)
VALUES 
    ('prod-001', 'Updated Widget A', 12.99, TIMESTAMP('2025-01-07 14:30:00'), 'UPDATE'),
    ('prod-002', 'Widget B', 15.99, TIMESTAMP('2025-01-07 14:30:00'), 'INSERT'),
    ('prod-004', NULL, NULL, TIMESTAMP('2025-01-07 14:30:00'), 'DELETE');

-- Complex merge with operation-based logic
MERGE `project.dataset.products` AS target
USING `project.dataset.products_temp_20250107_143000` AS source
ON target.product_id = source.product_id
WHEN MATCHED AND source.operation_type = 'DELETE' AND source.data_timestamp >= target.data_timestamp THEN
    DELETE
WHEN MATCHED AND source.operation_type IN ('UPDATE', 'UPSERT') AND source.data_timestamp >= target.data_timestamp THEN
    UPDATE SET 
        name = source.name,
        price = source.price,
        data_timestamp = source.data_timestamp,
        updated_at = CURRENT_TIMESTAMP()
WHEN NOT MATCHED AND source.operation_type IN ('INSERT', 'UPSERT') THEN
    INSERT (product_id, name, price, data_timestamp, created_at, updated_at)
    VALUES (source.product_id, source.name, source.price, source.data_timestamp, 
            CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP());

DROP TABLE `project.dataset.products_temp_20250107_143000`;
```

### Azure Data Lake (Synapse Analytics)

```sql
-- Step 1: Create temporary table
CREATE TABLE products_temp_20250107_143000 (
    product_id NVARCHAR(50) NOT NULL,
    name NVARCHAR(200) NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    data_timestamp DATETIME2(7) NOT NULL,
    created_at DATETIME2(7) DEFAULT GETUTCDATE(),
    updated_at DATETIME2(7) DEFAULT GETUTCDATE()
)
WITH (
    DISTRIBUTION = HASH(product_id),
    CLUSTERED COLUMNSTORE INDEX
);

-- Step 2: Insert data into temporary table
INSERT INTO products_temp_20250107_143000 
(product_id, name, price, data_timestamp)
VALUES 
    ('prod-001', 'Updated Widget A', 12.99, '2025-01-07 14:30:00'),
    ('prod-002', 'Widget B', 15.99, '2025-01-07 14:30:00'),
    ('prod-003', 'New Widget C', 8.99, '2025-01-07 14:30:00');

-- Step 3: Merge operation using Azure Synapse approach
-- Note: Synapse uses different syntax for MERGE
WITH source_data AS (
    SELECT 
        product_id,
        name,
        price,
        data_timestamp,
        ROW_NUMBER() OVER (PARTITION BY product_id ORDER BY data_timestamp DESC) as rn
    FROM products_temp_20250107_143000
)
MERGE products AS target
USING (SELECT * FROM source_data WHERE rn = 1) AS source
ON target.product_id = source.product_id
WHEN MATCHED AND source.data_timestamp >= target.data_timestamp THEN
    UPDATE SET 
        name = source.name,
        price = source.price,
        data_timestamp = source.data_timestamp,
        updated_at = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT (product_id, name, price, data_timestamp, created_at, updated_at)
    VALUES (source.product_id, source.name, source.price, source.data_timestamp, 
            GETUTCDATE(), GETUTCDATE());

-- Step 4: Drop temporary table
DROP TABLE products_temp_20250107_143000;
```

### Azure Data Lake with Partition Management

```sql
-- Create temporary table with partitioning
CREATE TABLE products_temp_20250107_143000 (
    product_id NVARCHAR(50) NOT NULL,
    name NVARCHAR(200) NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    data_timestamp DATETIME2(7) NOT NULL,
    partition_date DATE,
    batch_id NVARCHAR(50) DEFAULT 'batch_20250107_143000'
)
WITH (
    DISTRIBUTION = HASH(product_id),
    PARTITION (partition_date RANGE RIGHT FOR VALUES ('2025-01-01', '2025-02-01', '2025-03-01')),
    CLUSTERED COLUMNSTORE INDEX
);

INSERT INTO products_temp_20250107_143000 
(product_id, name, price, data_timestamp, partition_date)
VALUES 
    ('prod-001', 'Updated Widget A', 12.99, '2025-01-07 14:30:00', '2025-01-07'),
    ('prod-002', 'Widget B', 15.99, '2025-01-07 14:30:00', '2025-01-07'),
    ('prod-003', 'New Widget C', 8.99, '2025-01-07 14:30:00', '2025-01-07');

-- Partition-aware merge
MERGE products AS target
USING products_temp_20250107_143000 AS source
ON target.product_id = source.product_id 
   AND target.partition_date = source.partition_date
WHEN MATCHED AND source.data_timestamp >= target.data_timestamp THEN
    UPDATE SET 
        name = source.name,
        price = source.price,
        data_timestamp = source.data_timestamp,
        updated_at = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT (product_id, name, price, data_timestamp, partition_date, created_at, updated_at)
    VALUES (source.product_id, source.name, source.price, source.data_timestamp,
            source.partition_date, GETUTCDATE(), GETUTCDATE());

DROP TABLE products_temp_20250107_143000;
```

## Data Timestamp Logic for Conflict Resolution

### The Challenge

In distributed systems, data can arrive out of order. A record with timestamp 14:30 might arrive before a record with timestamp 14:25. Without proper timestamp checking, newer data could be overwritten by older data.

### Solution Pattern

Always compare `data_timestamp` values to ensure only newer or equal data overwrites existing records:

```sql
-- Correct: Only update if new data is newer or equal
WHEN MATCHED AND source.data_timestamp >= target.data_timestamp THEN
    UPDATE SET ...

-- Incorrect: Always update regardless of timestamp
WHEN MATCHED THEN
    UPDATE SET ...
```

### Advanced Timestamp Scenarios

```sql
-- Handle multiple timestamp scenarios
MERGE INTO products AS target
USING products_temp_20250107_143000 AS source
ON target.product_id = source.product_id
WHEN MATCHED THEN
    UPDATE SET 
        name = CASE 
            WHEN source.data_timestamp > target.data_timestamp THEN source.name
            ELSE target.name
        END,
        price = CASE 
            WHEN source.data_timestamp > target.data_timestamp THEN source.price
            ELSE target.price
        END,
        data_timestamp = CASE 
            WHEN source.data_timestamp > target.data_timestamp THEN source.data_timestamp
            ELSE target.data_timestamp
        END,
        updated_at = CURRENT_TIMESTAMP(),
        -- Track conflict resolution
        last_conflict_resolution = CASE 
            WHEN source.data_timestamp < target.data_timestamp THEN 'IGNORED_OLDER_DATA'
            WHEN source.data_timestamp = target.data_timestamp THEN 'DUPLICATE_TIMESTAMP'
            ELSE 'UPDATED_WITH_NEWER_DATA'
        END
WHEN NOT MATCHED THEN
    INSERT (product_id, name, price, data_timestamp, created_at, updated_at, last_conflict_resolution)
    VALUES (source.product_id, source.name, source.price, source.data_timestamp, 
            CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP(), 'NEW_RECORD');
```

## Automation and Error Handling

### Complete Workflow with Error Handling

```sql
-- Example: Databricks Delta with comprehensive error handling
BEGIN
    DECLARE temp_table_name STRING DEFAULT CONCAT('products_temp_', DATE_FORMAT(CURRENT_TIMESTAMP(), 'yyyyMMdd_HHmmss'));
    
    -- Step 1: Create temporary table
    EXECUTE IMMEDIATE CONCAT('
        CREATE OR REPLACE TABLE ', temp_table_name, ' (
            product_id STRING NOT NULL,
            name STRING NOT NULL,
            price DECIMAL(10,2) NOT NULL,
            data_timestamp TIMESTAMP NOT NULL
        ) USING DELTA
    ');
    
    -- Step 2: Insert data with validation
    EXECUTE IMMEDIATE CONCAT('
        INSERT INTO ', temp_table_name, '
        SELECT product_id, name, price, data_timestamp
        FROM VALUES 
            (''prod-001'', ''Updated Widget A'', 12.99, timestamp(''2025-01-07 14:30:00'')),
            (''prod-002'', ''Widget B'', 15.99, timestamp(''2025-01-07 14:30:00''))
        AS t(product_id, name, price, data_timestamp)
        WHERE product_id IS NOT NULL 
          AND name IS NOT NULL 
          AND price > 0
          AND data_timestamp IS NOT NULL
    ');
    
    -- Step 3: Validate data quality
    IF (SELECT COUNT(*) FROM temp_table_name WHERE product_id IS NULL OR name IS NULL) > 0 THEN
        THROW('Data quality check failed: NULL values detected');
    END IF;
    
    -- Step 4: Perform merge
    EXECUTE IMMEDIATE CONCAT('
        MERGE INTO products AS target
        USING ', temp_table_name, ' AS source
        ON target.product_id = source.product_id
        WHEN MATCHED AND source.data_timestamp >= target.data_timestamp THEN
            UPDATE SET 
                name = source.name,
                price = source.price,
                data_timestamp = source.data_timestamp,
                updated_at = CURRENT_TIMESTAMP()
        WHEN NOT MATCHED THEN
            INSERT (product_id, name, price, data_timestamp, created_at, updated_at)
            VALUES (source.product_id, source.name, source.price, source.data_timestamp, 
                    CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP())
    ');
    
    -- Step 5: Log operation results
    INSERT INTO operation_log (operation_type, table_name, records_processed, timestamp)
    SELECT 'MERGE_UPSERT', 'products', COUNT(*), CURRENT_TIMESTAMP()
    FROM temp_table_name;
    
    -- Step 6: Cleanup
    EXECUTE IMMEDIATE CONCAT('DROP TABLE ', temp_table_name);
    
EXCEPTION WHEN OTHER THEN
    -- Cleanup on error
    EXECUTE IMMEDIATE CONCAT('DROP TABLE IF EXISTS ', temp_table_name);
    THROW;
END;
```

## Best Practices Summary

### 1. Always Use Temporary Tables
- **Never insert directly** into target tables for bulk operations
- **Use timestamp-based naming** for temporary tables
- **Ensure atomic operations** through temp table pattern

### 2. Implement Proper Timestamp Logic
- **Compare data_timestamp** before updating existing records
- **Handle equal timestamps** appropriately (usually keep existing)
- **Log conflict resolutions** for auditing

### 3. Design for Idempotency
- **Operations should be safe to retry** without creating duplicates
- **Use deterministic temporary table names** when possible
- **Implement proper error handling** and cleanup

### 4. Monitor and Validate
- **Check data quality** before merge operations
- **Log all operations** for audit trails
- **Monitor for conflicts** and out-of-order data

### 5. Platform-Specific Considerations

| Platform | Key Considerations |
|----------|-------------------|
| **Databricks Delta** | Leverage ACID transactions, time travel capabilities |
| **BigQuery** | Consider slot usage, optimize for column-based operations |
| **Azure Synapse** | Account for distribution strategies, partition management |

## Industry Endorsements: LinkedIn's Data Infrastructure

**LinkedIn's Data Infrastructure Team** extensively uses staging table patterns for:
- **Kafka to data warehouse ETL processes**: Processing millions of member events daily
- **Large-scale member profile updates**: Handling profile changes across 900M+ members
- **A/B testing data collection and processing**: Safely merging experimental data

## Additional Staging Pattern Benefits

### Operational Advantages:
- **Rollback Capabilities**: Keep staging table if merge fails for troubleshooting
- **Monitoring and Auditing**: Track data processing pipeline stages and performance
- **Parallel Processing**: Multiple jobs can create separate staging tables simultaneously
- **Data Quality Validation**: Validate data before affecting production tables

This approach ensures data consistency, prevents duplicates, and handles concurrent operations safely in columnar database environments where traditional OLTP concurrency controls are not available.

---

**Navigation:**

- Previous: [Concurrency and key-value databases](./concurrency-key-value-dbs.md)
- Next: [Concurrency and databases summary](./concurrency-and-dbs-summary.md)
