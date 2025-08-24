# Concurrency and columnar databases

Columnar databases present unique challenges for concurrency control compared to traditional relational databases. Unlike OLTP systems, columnar databases often don't enforce primary key uniqueness constraints, and bulk operations are the norm rather than single-row transactions.

## The Staging Table Pattern

The recommended approach uses regular tables with temporary naming conventions to achieve idempotent, safe bulk operations regardless of retries or failures.

### Pattern Overview

1. **Create staging table** with unique timestamp-based suffix (e.g., `_temp_20250107_143000`)
2. **Insert data** into staging table
3. **Merge data** from staging table to target table
4. **Drop staging table** after successful merge

## Example: Databricks Delta

```sql
-- Step 1: Create "temporary" table with timestamp suffix
CREATE OR REPLACE TABLE products_temp_20250107_143000 (
    product_id STRING NOT NULL,
    name STRING NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    data_timestamp TIMESTAMP NOT NULL
) USING DELTA;
-- UNLIKE a real temporary table, this table supports distributed inserts e.g. from Spark jobs
-- and it remains present until explicitly dropped, allowing retries and searching for issues like duplicates in source data if needed.
-- The timestamp suffix ensures uniqueness even with concurrent processes.
-- Since this is a regular table, we can create indexes on it (after bulk insert completes) if needed for performance.
-- Don't forget to drop the table at the end of the process to avoid clutter.
-- If process fails before drop, a cleanup job can remove old staging tables.

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
        data_timestamp = source.data_timestamp
WHEN NOT MATCHED THEN
    INSERT (product_id, name, price, data_timestamp)
    VALUES (source.product_id, source.name, source.price, source.data_timestamp);

-- Step 4: Drop temporary table
DROP TABLE products_temp_20250107_143000;
```

## Key Benefits

- **No duplicates regardless of retries** - staging table pattern is idempotent
- **Atomic operation** - all data succeeds or fails together
- **Handles both inserts and updates** using MERGE operations
- **Protects against overwriting newer data** with timestamp comparison
- **Compatible with distributed processing** - multiple processes can create separate staging tables

## Data Timestamp Logic

Always compare `data_timestamp` values to ensure only newer or equal data overwrites existing records:

```sql
-- Correct: Only update if new data is newer or equal
WHEN MATCHED AND source.data_timestamp >= target.data_timestamp THEN
    UPDATE SET ...

-- Incorrect: Always update regardless of timestamp
WHEN MATCHED THEN
    UPDATE SET ...
```

## Best Practices

1. **Always Use Staging Tables** - Never insert directly into target tables for bulk operations
2. **Use timestamp-based naming** for staging tables to ensure uniqueness
3. **Implement proper timestamp logic** to prevent overwriting newer data with older data
4. **Design for idempotency** - operations should be safe to retry without creating duplicates
5. **Clean up staging tables** after successful operations

The staging table pattern ensures data consistency, prevents duplicates, and handles concurrent operations safely in columnar database environments.

---

**Navigation:**

- Previous: [Concurrency and key-value databases](./concurrency-key-value-dbs.md)
- Next: [Concurrency and databases summary](./concurrency-and-dbs-summary.md)
