# Indexing

Indices enable fast searching of data in databases, they are used in both relational databases and document databases.

Without indices, the database has to perform a **scan** - a search row by row / document by document for results, with a complexity of O(N), where N is the number of rows/documents in the table/collection.

With indices, the database can perform a **seek** - search within a tree-structure for finding results, with a complexity of O(log(N)).

Primary keys are automatically indexed, enabling fast searching for records/documents using their ID.

An index can contain one column/field or more (composite index).

The more indices you define (and more columns you put in an index), the larger the storage required for these indices and the higher the write latency becomes, so don't index everything blindly.

## Performance Impact Example

Consider a table with 1 million users:

**Without Index (Table Scan)**:

```sql
-- SQL Server - searches all 1 million rows
SELECT * FROM Users WHERE Email = 'john@example.com';
-- Execution: ~1000ms, reads 1,000,000 rows
```

**With Index (Index Seek)**:

```sql
-- SQL Server - uses index to jump directly to the row
CREATE INDEX IX_Users_Email ON Users(Email);
SELECT * FROM Users WHERE Email = 'john@example.com';
-- Execution: ~1ms, reads 1 row
```

## Index Types by Database

### SQL Server Index Types

#### Clustered Index

- **One per table** - determines physical storage order
- **Primary key** automatically creates clustered index
- **Best for**: Range queries, ORDER BY operations

```sql
-- Clustered index on primary key (automatic)
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY, -- Clustered index created automatically
    Email NVARCHAR(255),
    CreatedDate DATETIME2
);
```

#### Non-Clustered Index

- **Multiple per table** - points to clustered index or heap
- **Best for**: Equality searches, covering queries

```sql
-- Simple non-clustered index
CREATE INDEX IX_Users_Email ON Users(Email);

-- Composite index (multiple columns)
CREATE INDEX IX_Users_Email_CreatedDate ON Users(Email, CreatedDate);

-- Covering index (includes additional columns)
CREATE INDEX IX_Users_Email_Covering 
ON Users(Email) 
INCLUDE (FirstName, LastName, CreatedDate);
```

### MongoDB Index Types

#### Single Field Index

```javascript
// MongoDB - Single field index (background creation prevents blocking)
db.users.createIndex({ "email": 1 }, { background: true }); // 1 = ascending, -1 = descending

// Query that uses the index
db.users.find({ "email": "john@example.com" });
```

#### Compound Index

```javascript
// MongoDB - Compound index (order matters!, background creation prevents blocking)
db.users.createIndex({ "status": 1, "createdDate": -1 }, { background: true });

// Queries that can use this index:
db.users.find({ "status": "active" }); // Uses index
db.users.find({ "status": "active", "createdDate": { $gte: ISODate("2024-01-01") } }); // Uses index
db.users.find({ "createdDate": { $gte: ISODate("2024-01-01") } }); // Cannot use index efficiently
```

#### Text Index

```javascript
// MongoDB - Text search index (background creation prevents blocking)
db.users.createIndex({ "firstName": "text", "lastName": "text" }, { background: true });

// Text search query
db.users.find({ $text: { $search: "john smith" } });
```

## Rules for creating indices

Rules for creating indices:

1. Look at the queries you run, from most common to least common:
    1. For each of these queries, which columns/fields do they use in their filter (where clause)?
        1. For each of these columns, how well do the values divide the data - for instance `status = 'SHIPPED'` filters to ~20% of records, `date_value = '2020-01-01'` filters-out most of the data (assuming the data isn't all from one day). If the column is effective in filtering out the data, include it in your index.
        2. Sort the columns/fields you need for the query from the one with the highest filtering to the one with the lowest filtering, this usually is the optimal order for the fields in your index. Take into account that if you use a field for equality (`=`) it filters out much more data than if you use it as a range (`>=`, `>`, `<=`, `<`).
    2. Did you reach similar indices for 2 different queries e.g. `index1: col1, col2, col3, col4`, `index2: col1, col3, col4` where index1 is index2 with additional columns? - If so you can discard index2, because searching for `col1=A, col2=<anything>, col3=C, col4=D` is the same as searching for `col1=A, col2=B, col3=C, col4=D`.
    3. Which columns/fields do they use in their project (select clause)? - In some relational database types you can add "include columns" to the end of your index, these are columns the database can use to return a result directly from the index, without actually reading the row. This means that if you often use an index to look at a few specific columns in the table, the database can save time if you include these specific columns in the index
2. For Document Databases e.g. MongoDB, is a field you are using in queries mostly undefined or null? Are you searching only for the defined non-null values? In this case a sparse index may help you - see [Sparse Indices](#spare-indices) for details, when and how to use these and what to be careful about.

### Examples

```sql
-- SQL Server example: Query analysis and index creation
-- Step 1.1: Identify columns used in WHERE clause
SELECT UserId, FirstName, LastName 
FROM Users 
WHERE Status = 'Active'           -- Medium selectivity (~80% of users)
  AND CreatedDate >= '2024-01-01' -- High selectivity (~5% of users)
  AND City = 'New York'           -- Medium selectivity (~15% of users)
ORDER BY CreatedDate DESC;

-- Step 1.1.1: Analyze column selectivity
-- CreatedDate >= '2024-01-01' filters to ~5% (highest filtering)
-- City = 'New York' filters to ~15% (medium filtering)  
-- Status = 'Active' filters to ~80% (lowest filtering)

-- Step 1.1.2: Order by highest to lowest filtering (equality before range)
CREATE INDEX IX_Users_Optimal ON Users(City, Status, CreatedDate);
-- City (equality, 15% selectivity) -> Status (equality, 80% selectivity) -> CreatedDate (range)

-- Step 1.3: Add include columns for SELECT clause
CREATE INDEX IX_Users_Covering ON Users(City, Status, CreatedDate) 
INCLUDE (FirstName, LastName); -- Avoid key lookups
```

```csharp
// MongoDB C# example: Same principles apply
public async Task<List<User>> GetActiveUsersByCity(string city)
{
    // Query uses: city (equality, high selectivity), status (equality, medium selectivity), createdDate (range)
    return await _users.Find(u => 
        u.City == city &&                                    // Equality, high selectivity
        u.Status == "active" &&                             // Equality, medium selectivity  
        u.CreatedDate >= DateTime.Today.AddDays(-30)        // Range, after equalities
    ).ToListAsync();
}

// Create compound index with optimal column order (background creation prevents blocking)
await _users.Indexes.CreateOneAsync(
    new CreateIndexModel<User>(
        Builders<User>.IndexKeys
            .Ascending(u => u.City)        // Highest selectivity equality first
            .Ascending(u => u.Status)      // Medium selectivity equality second
            .Descending(u => u.CreatedDate), // Range query last
        new CreateIndexOptions { Background = true }
    )
);
```

```sql
-- Step 1.2: Index consolidation example
-- These queries have similar index needs:
-- Query A: WHERE Email = ? AND Status = ? AND CreatedDate >= ?
-- Query B: WHERE Email = ? AND Status = ?

-- Instead of creating two indexes:
CREATE INDEX IX_Users_Email_Status ON Users(Email, Status);           -- For Query B
CREATE INDEX IX_Users_Email_Status_Date ON Users(Email, Status, CreatedDate); -- For Query A

-- Create only the comprehensive one (covers both queries):
CREATE INDEX IX_Users_Comprehensive ON Users(Email, Status, CreatedDate);
-- This index can handle both Query A and Query B efficiently
```

#### RelationalDB Examples

##### C#: Entity Framework Core Index Configuration

```csharp
// Entity configuration
public class User
{
    public int UserId { get; set; }
    public string Email { get; set; }
    public string Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public string City { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Single column index
        builder.HasIndex(u => u.Email)
               .HasDatabaseName("IX_Users_Email")
               .IsUnique();

        // Composite index
        builder.HasIndex(u => new { u.Status, u.CreatedDate })
               .HasDatabaseName("IX_Users_Status_CreatedDate");

        // Covering index (SQL Server specific)
        builder.HasIndex(u => new { u.Status, u.City })
               .HasDatabaseName("IX_Users_Status_City")
               .IncludeProperties(u => new { u.Email, u.CreatedDate });
    }
}
```

#### Document Database Examples

##### C#: MongoDB C# Driver Index Creation

```csharp
using MongoDB.Driver;
using MongoDB.Bson;

public class UserService
{
    private readonly IMongoCollection<User> _users;

    public UserService(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("users");
    }

    public async Task CreateIndexesAsync()
    {
        // Single field index (background creation prevents blocking)
        await _users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Background = true }
            )
        );

        // Compound index (background creation prevents blocking)
        await _users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys
                    .Ascending(u => u.Status)
                    .Descending(u => u.CreatedDate),
                new CreateIndexOptions { Background = true }
            )
        );

        // Text index for search (background creation prevents blocking)
        await _users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys
                    .Text(u => u.FirstName)
                    .Text(u => u.LastName),
                new CreateIndexOptions { Background = true }
            )
        );

    }

    // Query using indexes
    public async Task<List<User>> GetActiveUsersByCity(string city)
    {
        // This query will use the compound index on Status, CreatedDate
        return await _users.Find(u => 
            u.Status == "active" && 
            u.City == city &&
            u.CreatedDate >= DateTime.Today.AddDays(-30)
        ).ToListAsync();
    }
}
```

## Query Performance Analysis

### SQL Server - Using Execution Plans

```csharp
using System.Data.SqlClient;
using System.Diagnostics;
using Dapper;

public class QueryAnalyzer
{
    public async Task<QueryStats> AnalyzeQueryAsync(string connectionString, string query)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Enable statistics
        await connection.ExecuteAsync("SET STATISTICS IO ON");
        await connection.ExecuteAsync("SET STATISTICS TIME ON");

        var stopwatch = Stopwatch.StartNew();
        var results = await connection.QueryAsync(query);
        stopwatch.Stop();

        // Get execution plan
        var planQuery = (
            $"SELECT query_plan FROM sys.dm_exec_query_plan(plan_handle) "
            + $"FROM sys.dm_exec_requests WHERE session_id = @@SPID"
        );
        
        var executionPlan = await connection.QueryFirstOrDefaultAsync<string>(planQuery);

        return new QueryStats
        {
            ExecutionTime = stopwatch.ElapsedMilliseconds,
            RowCount = results.Count(),
            ExecutionPlan = executionPlan
        };
    }
}

public class QueryStats
{
    public long ExecutionTime { get; set; }
    public int RowCount { get; set; }
    public string ExecutionPlan { get; set; }
}
```

### MongoDB - Query Explanation

```csharp
public class MongoQueryAnalyzer
{
    private readonly IMongoCollection<User> _users;

    public MongoQueryAnalyzer(IMongoCollection<User> users)
    {
        _users = users;
    }

    public async Task<BsonDocument> ExplainQueryAsync()
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Status, "active"),
            Builders<User>.Filter.Gte(u => u.CreatedDate, DateTime.Today.AddDays(-30))
        );

        // Get query execution statistics
        var explanation = await _users
            .Find(filter)
            .Explain(ExplainVerbosity.ExecutionStats);

        return explanation;
    }

    public async Task<IndexAnalysisResult> AnalyzeIndexUsage()
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Status, "active"),
            Builders<User>.Filter.Eq(u => u.City, "New York")
        );

        var explanation = await _users
            .Find(filter)
            .Explain(ExplainVerbosity.ExecutionStats);

        var executionStats = explanation["executionStats"];
        
        return new IndexAnalysisResult
        {
            Stage = executionStats["stage"].AsString,
            DocsExamined = executionStats["totalDocsExamined"].AsInt32,
            DocsReturned = executionStats["totalDocsReturned"].AsInt32,
            ExecutionTimeMillis = executionStats["executionTimeMillis"].AsInt32
        };
    }
}

public class IndexAnalysisResult
{
    public string Stage { get; set; }
    public int DocsExamined { get; set; }
    public int DocsReturned { get; set; }
    public int ExecutionTimeMillis { get; set; }
}
```

## Spare indices

MongoDB supports sparse indices, these indices only "point" to the documents that have a defined, non-null value for a field.

```javascript
// MongoDB - Sparse index (only indexes documents that have the field, background creation prevents blocking)
db.users.createIndex({ "phoneNumber": 1 }, { sparse: true, background: true });
```

```csharp
// Sparse index for optional fields (only indexes documents that have the field, background creation prevents blocking)
await _users.Indexes.CreateOneAsync(
    new CreateIndexModel<User>(
        Builders<User>.IndexKeys.Ascending(u => u.PhoneNumber),
        new CreateIndexOptions { Sparse = true, Background = true }
    )
);
```

### WHEN TO USE

When the field is mostly null/missing in your collection and non-null values are the LESS COMMON cases

In these cases:

- The null/missing values (being common) don't gain advantage from being indexed
- Using a sparse index saves significant index space and improves write performance, because the index ignores the null/missing values.

For example scenario: If 80% of users don't have phone numbers and you want to be able to search by phone number
A regular index: indexes 1,000,000 documents (including 800,000 nulls)
A sparse index: indexes only 200,000 documents (only those with phone numbers)

Queries that benefit from sparse index (finding the uncommon non-null cases):

```javascript
db.users.find({ "phoneNumber": "+1-555-0123" }); // Uses sparse index efficiently
db.users.find({ "phoneNumber": { $exists: true } }); // Uses sparse index efficiently
db.users.find({ "phoneNumber": { $ne: null } }); // Uses sparse index efficiently
```

### WHEN **NOT** TO USE

When you want to be able to search for only undefined (missing) or null values

When searching for null values, MongoDB will perform a full collection scan
because null documents are not included in the sparse index

Queries that DON'T use sparse index:

```javascript
db.users.find({ "phoneNumber": null }); // Full collection scan - nulls not in index
db.users.find({ "phoneNumber": { $exists: false } }); // Full collection scan
db.users.find({ "phoneNumber": { $in: [null, "+1-555-0123"] } }); // Full collection scan (due to null)
```

Use sparse indexes carefully: they're perfect for "find the few that have X"
but problematic for "find the many that don't have X" queries

## Common Index Anti-Patterns

### 1. Over-Indexing

```sql
-- BAD: Too many indexes on same table
CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_FirstName ON Users(FirstName);
CREATE INDEX IX_Users_LastName ON Users(LastName);
CREATE INDEX IX_Users_City ON Users(City);
CREATE INDEX IX_Users_Status ON Users(Status);
-- Result: Slow INSERTs/UPDATEs, excessive storage

-- GOOD: Strategic composite indexes
CREATE INDEX IX_Users_Search ON Users(Status, City) INCLUDE (Email, FirstName, LastName);
```

### 2. Wrong Column Order

```sql
-- BAD: Low selectivity column first
CREATE INDEX IX_Orders_Poor ON Orders(Status, CustomerId, OrderDate);

-- GOOD: High selectivity column first
CREATE INDEX IX_Orders_Good ON Orders(CustomerId, OrderDate, Status);
```

### 3. Missing Covering Columns

```sql
-- Query that causes key lookups
SELECT CustomerId, OrderDate, TotalAmount 
FROM Orders 
WHERE Status = 'Pending';

-- Index doesn't cover all selected columns
CREATE INDEX IX_Orders_Status ON Orders(Status);
-- Result: Index seek + key lookup for each row

-- BETTER: Covering index eliminates key lookups
CREATE INDEX IX_Orders_Status_Covering ON Orders(Status) INCLUDE (CustomerId, OrderDate, TotalAmount);
```

## Index Maintenance

### SQL Server Index Maintenance

```sql
-- Check index fragmentation
SELECT 
    i.name AS IndexName,
    s.avg_fragmentation_in_percent,
    s.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') s
JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE s.avg_fragmentation_in_percent > 10;

-- Rebuild highly fragmented indexes
ALTER INDEX IX_Users_Email ON Users REBUILD;

-- Update statistics
UPDATE STATISTICS Users;
```

### MongoDB Index Maintenance

```javascript
// Check index usage
db.users.aggregate([{ $indexStats: {} }]);

// Drop unused indexes by name or specification
db.users.dropIndex("email_1");  // Drop by name
db.users.dropIndex({ "email": 1 }); // Drop by specification

// Rebuild indexes (rarely needed)
db.users.reIndex();
```

## Best Practices Summary

1. **Start with query analysis** - understand your workload first
2. **Index your WHERE clauses** - focus on filter conditions
3. **Order columns by selectivity** - highest selectivity first
4. **Use covering indexes** for frequently accessed columns
5. **Consolidate similar indexes** - avoid redundancy
6. **Monitor index usage** - drop unused indexes
7. **Test with realistic data volumes** - performance changes with scale
8. **Consider write performance** - indexes slow down INSERTs/UPDATEs
9. **Use sparse indexes (MongoDB)** - for optional fields where most documents don't have the field and queries never search for null values

If you have a long-running query, profile/inspect it to see how it searches for the data - does it use seek or scan? See if the fields it uses are indexed and if creating a composite index according to the above rules could help it find the data by filtering-out rows/documents faster.

---

**Navigation:**

- Previous: [Data modelling summary](./data-modelling-summary.md)
- Next: [Concurrency and databases](./concurrency-and-dbs.md)
