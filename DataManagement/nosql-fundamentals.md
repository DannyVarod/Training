# NoSQL Fundamentals for SQL Developers

---

This section provides essential background on NoSQL concepts for developers coming from a relational database background.

## Key Conceptual Differences

### 1. Schema Flexibility vs. Schema Enforcement

**SQL (What you know)**:
```sql
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Email NVARCHAR(200) NOT NULL
);
-- Schema is enforced at database level
```

**NoSQL (New concept)**:
```javascript
// MongoDB - No predefined schema
db.users.insertOne({
    _id: "user1",
    name: "John",
    email: "john@example.com",
    preferences: { theme: "dark", language: "en" } // Can add nested data
});

db.users.insertOne({
    _id: "user2", 
    name: "Jane",
    // email field is missing - still allowed
    age: 25,
    hobbies: ["reading", "gaming"] // Different structure
});
```

### 2. Normalization vs. Denormalization

**SQL Approach (Normalized)**:
```sql
-- Users table
Id | Name | Email
1  | John | john@example.com

-- UserAddresses table  
UserId | Street     | City
1      | 123 Main   | Boston
1      | 456 Oak    | Seattle
```

**NoSQL Approach (Denormalized)**:
```javascript
// Everything in one document
{
    _id: "user1",
    name: "John",
    email: "john@example.com",
    addresses: [
        { street: "123 Main", city: "Boston" },
        { street: "456 Oak", city: "Seattle" }
    ]
}
```

### 3. ACID vs. BASE

**ACID (SQL - What you know)**:
- **Atomicity**: All or nothing transactions
- **Consistency**: Data integrity rules enforced
- **Isolation**: Concurrent transactions don't interfere
- **Durability**: Committed data survives system failures

**BASE (NoSQL - New concept)**:
- **Basically Available**: System remains operational
- **Soft state**: Data may change over time (eventual consistency)
- **Eventually consistent**: System will become consistent over time

## Key NoSQL Categories

### 1. Document Databases (MongoDB, CosmosDB)
- **What**: Store data as documents (JSON-like)
- **Think of it as**: Storing entire C# objects as JSON
- **Good for**: Complex nested data, flexible schemas

### 2. Key-Value Stores (Redis, DynamoDB)
- **What**: Simple key → value mapping
- **Think of it as**: A massive `Dictionary<string, object>` in memory
- **Good for**: Caching, sessions, simple lookups

### 3. Wide-Column (Cassandra, HBase)
- **What**: Like tables, but columns can vary per row
- **Think of it as**: SQL tables where each row can have different columns
- **Good for**: Time-series data, high write volumes

### 4. Columnar (BigQuery, Snowflake)
- **What**: Store data column-wise instead of row-wise
- **Think of it as**: SQL optimized for analytics instead of transactions
- **Good for**: Data warehousing, reporting, analytics

## Common Patterns You'll Encounter

### 1. Eventual Consistency
```csharp
// In SQL, this is immediate:
using var transaction = connection.BeginTransaction();
// All changes are immediately consistent

// In NoSQL, updates may propagate over time:
await userCollection.UpdateOneAsync(filter, update);
// Other nodes might see old data for a few milliseconds
```

### 2. Partition Keys (Sharding)
```csharp
// SQL: Database handles data distribution
// NoSQL: You often specify how data is distributed

// Good partition key (distributes evenly)
var partitionKey = $"user_{userId.GetHashCode() % 10}";

// Bad partition key (creates hotspots)  
var partitionKey = $"region_{userRegion}"; // All US users on one partition
```

### 3. Denormalized Queries
```csharp
// SQL: Join across tables
var query = from u in users
           join o in orders on u.Id equals o.UserId
           select new { u.Name, o.Total };

// NoSQL: Pre-join data in document
var userDoc = {
    name: "John",
    orders: [
        { id: "order1", total: 100.00 },
        { id: "order2", total: 150.00 }
    ]
};
```

## Performance Characteristics

### Scalability Patterns

**SQL (Vertical Scaling)**:
- Add more CPU/RAM to existing server
- Eventually hits hardware limits
- Strong consistency across all operations

**NoSQL (Horizontal Scaling)**:
- Add more servers to cluster
- Can scale almost infinitely
- Trade consistency for availability

### Query Performance

**SQL Strengths**:
- Complex joins and aggregations
- Ad-hoc queries with WHERE clauses
- Mature query optimizers

**NoSQL Strengths**:
- Simple key-based lookups (microseconds)
- Bulk read/write operations
- Predictable performance at scale

## Developer Mental Model Shifts

### 1. From Relations to Documents
```csharp
// SQL mindset: Separate entities, join when needed
public class User { }
public class Order { }
// Query: JOIN users u ON orders.user_id = u.id

// NoSQL mindset: Embed related data
public class UserWithOrders 
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<Order> Orders { get; set; } // Embedded
}
```

### 2. From Schema-First to Data-First
```csharp
// SQL: Define schema, then insert data
CREATE TABLE Products (...);
INSERT INTO Products VALUES (...);

// NoSQL: Insert data, schema emerges
var product = new { 
    name = "iPhone", 
    specs = new { screen = "6.1 inch", storage = "128GB" }
};
// No predefined schema needed
```

### 3. From ACID to Idempotency
```csharp
// SQL: Rely on transactions
using var tx = connection.BeginTransaction();
// Complex multi-table operations

// NoSQL: Design idempotent operations
var update = Builders<User>.Update
    .Set(u => u.LastLogin, DateTime.UtcNow)
    .Inc(u => u.LoginCount, 1);
// Safe to retry without side effects
```

## Common Gotchas for SQL Developers

### 1. No Foreign Keys
```csharp
// SQL: Database prevents orphaned records
DELETE FROM Users WHERE Id = 1; -- Fails if orders exist

// NoSQL: Application must handle referential integrity
await users.DeleteOneAsync(u => u.Id == "user1");
// Orphaned orders may remain - your code must clean up
```

### 2. No Complex Queries
```sql
-- SQL: Complex analysis queries work great
SELECT region, AVG(order_total), COUNT(*)
FROM orders o
JOIN users u ON o.user_id = u.id  
WHERE o.created_date > '2023-01-01'
GROUP BY u.region
HAVING COUNT(*) > 100;
```

```javascript
// NoSQL: Often requires multiple queries or pre-aggregation
// May need to denormalize data or use separate analytics database
```

### 3. Consistency Models
```csharp
// SQL: Read-after-write consistency guaranteed
context.Users.Add(newUser);
context.SaveChanges();
var user = context.Users.Find(newUser.Id); // Always finds the user

// NoSQL: May have read-after-write delays
await collection.InsertOneAsync(newUser);
var user = await collection.Find(u => u.Id == newUser.Id).FirstOrDefaultAsync();
// Might return null for a few milliseconds
```

## When to Consider NoSQL

### Good Signals:
- Storing JSON/XML in SQL columns
- Frequent schema changes
- Horizontal scaling needs
- Simple key-based access patterns
- High read/write volumes

### Warning Signals:
- Need for complex reporting
- Strong consistency requirements
- Small data volumes (< 1M records)
- Heavy use of JOINs and aggregations

## Next Steps

Now that you understand NoSQL fundamentals, you can dive into:
- [Database types](./database-types.md) - Specific NoSQL categories
- [Data modelling](./data-modelling-db-types.md) - How to design for each type
- [Concurrency](./concurrency-and-dbs.md) - How concurrency differs in NoSQL

---

**Navigation:**

- Next page: [Database types (and alike)](./database-types.md)
