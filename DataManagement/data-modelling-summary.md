# Data modelling summary

---

This section provides a comprehensive comparison of data modelling approaches across different database types to help you choose the right strategy for your specific use case.

## Core Principles by Database Type

### Relational Databases: Normalize Data

**Key Principle**: Split data into multiple tables with no duplication

**Approach**:
- Create separate tables for related entities
- Use foreign keys to maintain relationships
- Normalize to eliminate data redundancy
- Enforce data consistency through constraints

**Example Structure**:
```
Students table: student_id, first_name, last_name, birthdate
EmailAddresses table: email_address, student_id
StudentVehicles table: student_id, vehicle_license_plate
VehicleDetails table: vehicle_license_plate, manufacturer
```

**Why**: Relational databases excel at maintaining data consistency and supporting complex transactions across multiple tables.

### Document Databases: Denormalize Data

**Key Principle**: Store all related data in a single document with duplication

**Approach**:
- Embed related data as nested objects and arrays
- Duplicate values across documents when needed
- Use projection to fetch only required fields
- One object type per collection

**Example Structure**:
```json
{
  "student_id": "uuid",
  "first_name": "John",
  "last_name": "Smith",
  "email_addresses": ["email1", "email2"],
  "vehicles": [
    {"license_plate": "123", "manufacturer": "Honda"}
  ]
}
```

**Why**: Document databases optimize for fast single-document operations and flexible schemas.

### Columnar Databases: Flatten Everything

**Key Principle**: Completely denormalize into flat tables with massive duplication

**Approach**:
- Store all data in wide, flat tables
- Duplicate fields extensively across rows
- Use nested objects and arrays when supported
- No joins - everything in one table per entity type

**Example Structure**:
```
Single table: student_id, first_name, last_name, email_addresses[], vehicles[]
```

**Why**: Columnar storage and compression make duplication efficient, while avoiding joins improves analytical query performance.

### Key-Value Databases: Purpose-Built Keys

**Key Principle**: Design keys and data structures for specific use cases

**Approach**:
- Use hierarchical key naming (app:type:id:field)
- Choose appropriate value types (strings, lists, hashsets, etc.)
- Design for specific access patterns
- Consider TTL and expiration needs

**Example Patterns**:
```
Cache: "app:cache:user:123" → JSON blob
Counter: "app:events:login:user:123:count" → integer
Time window: "app:throttling:api:bucket:2025-01-01-14" → list
```

**Why**: Key-value stores optimize for simple, fast operations with predictable access patterns.

## Primary Key Best Practices (Universal)

### Do NOT Use
- ❌ Auto-increment integers
- ❌ Database-generated UUIDs
- ❌ Composite primary keys (multiple columns)

### DO Use
- ✅ Application-generated UUIDs for entities
- ✅ Meaningful string IDs for dictionaries
- ✅ ISO standards when applicable (country codes, etc.)
- ✅ Readable, short identifiers

### Examples of Good Dictionary IDs
```
Countries: "US", "CA", "GB"
Hobbies: "chess", "rock_climbing", "poker_texas"
Status: "active", "pending", "suspended"
```

**Rationale**: Consistent single-column primary keys enable better tooling, idempotency, and cross-environment consistency.

## Data Types and Schema Design

### Type Selection Guidelines

| Database Type | Schema Flexibility | Type Enforcement | Best For |
|---------------|-------------------|------------------|----------|
| **Relational** | Rigid schema | Strict typing | Structured, consistent data |
| **Document** | Flexible schema | Dynamic typing | Semi-structured, evolving data |
| **Columnar** | Defined schema | Mixed support | Analytics, large datasets |
| **Key-Value** | No schema | Value-dependent | Simple key-based access |

### Cross-Database Compatibility

When designing for multiple database types:
1. **Use consistent data types** across systems
2. **Avoid database-specific types** when possible
3. **Plan for type conversion** in ETL processes
4. **Document type mappings** between systems

## Message and File Storage Patterns

### Message Data Modelling

**Serialization Choices**:
- **JSON**: Best for cross-language compatibility and flexibility

**Ordering Considerations**:
- **Message Queues**: Guaranteed order within queue
- **Message Topics**: Order guaranteed per partition key

**Partitioning Strategy**:
```
Required partitions = (Peak throughput × Growth factor × Processing time) + Buffer
Example: (10M/hour × 5 × 10ms) + Buffer = ~20 partitions
```

### Distributed File Storage

**Organization Strategies**:
- **Time-based partitioning**: `/year/month/day/hour/`
- **Hash-based partitioning**: Distribute evenly
- **Access pattern-based**: Optimize for read/write patterns

**Format Selection**:
- **Parquet/ORC**: Columnar formats for analytics
- **Avro/JSON**: Row-based for transactional data
- **Compression**: Balance size vs processing speed

## Decision Matrices

### Data Modeling Strategy by Database Type

| Database Type | Modeling Approach | Primary Key Strategy | Relationships | Schema Evolution |
|---------------|-------------------|---------------------|---------------|------------------|
| **Relational** | Normalize (3NF) | Application-generated GUID | Foreign keys, JOINs | Migration scripts |
| **Document** | Denormalize (embed) | Application-generated GUID | Embedded documents/arrays | Document versioning |
| **Columnar** | Flat/wide tables | Application-generated GUID | Denormalized JOINs | ALTER table operations |
| **Key-Value** | Purpose-built keys | Hierarchical key structure | Key naming conventions | Key versioning |

### Primary Key Selection Matrix

| Scenario | Recommended Approach | Example | Why |
|----------|---------------------|---------|-----|
| **Business Entities** | Application-generated GUID | `Guid.NewGuid()` | Cross-environment consistency, idempotency |
| **Dictionary/Lookup Tables** | Meaningful string IDs | `"US"`, `"active"`, `"chess"` | Human-readable, self-documenting |
| **Composite Keys Required** | Concatenated string | `"user123/order456"` | Single column, maintains relationships |
| **Temporal Data** | GUID + timestamp suffix | `"event_{guid}_{timestamp}"` | Ordering + uniqueness |

### Data Structure Decision Matrix

| Data Characteristics | Relational | Document | Columnar | Key-Value |
|---------------------|------------|----------|----------|-----------|
| **Fixed schema, simple relationships** | ✅ Excellent | ❌ Overkill | ❌ Overkill | ❌ Wrong tool |
| **Nested data, flexible schema** | ❌ Poor fit | ✅ Excellent | ⚠️ Possible | ❌ Limited |
| **High read volume, simple queries** | ⚠️ Possible | ✅ Good | ✅ Excellent | ✅ Excellent |
| **Complex analytics, aggregations** | ⚠️ Limited | ❌ Poor | ✅ Excellent | ❌ Wrong tool |
| **Simple key-value lookups** | ❌ Overkill | ❌ Overkill | ❌ Overkill | ✅ Excellent |

### Choose Normalization (Relational) When:
- ACID transactions are required
- Data consistency is critical
- Complex relationships exist
- Multiple applications need consistent views

### Choose Denormalization (Document/Columnar) When:
- Read performance is priority
- Schema changes frequently
- Nested data structures are common
- Single-application data access

### Choose Specialized Patterns (Key-Value/Messages/Files) When:
- Specific access patterns are known
- Scale requirements are extreme
- Simple operations dominate
- Caching or streaming is primary use case

## Common Anti-Patterns

### Universal Anti-Patterns
- Using auto-increment IDs across environments
- Storing JSON/XML in relational columns
- Over-normalizing document databases
- Under-partitioning message topics

## Why These Anti-Patterns Are Problematic

### Auto-Increment IDs: Hidden Costs
Beyond basic uniqueness issues:
- **Cross-Environment Migration**: Dev/staging/prod have different IDs for same logical records
- **Microservice Complications**: Services can't generate IDs independently
- **Backup/Restore Issues**: ID sequences need special handling during recovery
- **Loss of Idempotency**: Cannot safely retry operations without creating duplicates
- **ETL Pipeline Complexity**: Requires complex mapping tables for data synchronization

### Composite Primary Keys: Infrastructure Problems
Additional hidden costs:
- **ORM Limitations**: Many ORMs struggle with composite keys, requiring custom handling
- **Performance Impact**: Larger index sizes, slower joins, more memory usage
- **API Complexity**: REST endpoints become unwieldy with multiple ID parameters
- **Tooling Breakdown**: Most data processing tools expect single-column primary keys
- **Query Complexity**: More complex WHERE clauses and JOIN conditions

## Common C# Developer Mistakes

### 8. "EF Core Patterns Work Everywhere"
**Misconception**: Entity Framework patterns apply to all databases
**Reality**: Each database type requires different modeling approaches
**Example**: Using navigation properties in MongoDB (doesn't translate well)
**Fix**: Learn database-specific drivers and patterns (MongoDB.Driver, StackExchange.Redis)

### 9. "LINQ Works with All Databases"
**Misconception**: LINQ-to-X means identical querying across database types
**Reality**: Each database has different query capabilities and limitations
**Example**: Complex LINQ joins don't work well with MongoDB aggregation pipelines
**Fix**: Use native query methods for optimal performance

### 10. "GUIDs Are Always the Right Choice"
**Misconception**: Since auto-increment is bad, always use GUIDs
**Reality**: Dictionary tables benefit from meaningful string IDs
**Example**: Using GUIDs for country codes instead of "US", "CA", "GB"
**Fix**: Use GUIDs for business entities, meaningful strings for lookups

### 11. "NoSQL Means No Relationships"
**Misconception**: NoSQL databases can't handle related data
**Reality**: Relationships exist but are modeled differently (embedding vs. referencing)
**Example**: Storing user IDs in separate collections instead of embedding user data
**Fix**: Design for access patterns, embed frequently accessed related data

### 12. "Async/Await Solves Concurrency"
**Misconception**: Using async methods prevents database concurrency issues
**Reality**: Async helps with thread utilization, not data integrity
**Example**: Multiple async operations updating the same record still cause lost updates
**Fix**: Use proper concurrency patterns (optimistic, atomic operations)

### Database-Specific Anti-Patterns

**Relational**:
- Storing arrays or objects in columns
- Creating overly complex join queries
- Using GUIDs for small dictionary tables
- Ignoring Entity Framework concurrency tokens
- Using `context.Database.ExecuteSqlRaw()` for everything

**Document**:
- Splitting related data across collections
- Creating one collection for all entity types
- Ignoring projection capabilities
- Using MongoDB like a relational database with JOINs
- Not leveraging embedded documents for performance

**Columnar**:
- Joining across multiple tables
- Frequent single-row updates
- Real-time transactional requirements
- Using columnar databases for operational workloads
- Ignoring partitioning strategies

**Key-Value**:
- Scanning keys instead of direct access
- Storing complex nested data
- Using for complex query requirements
- Not leveraging different Redis data types
- Storing everything as JSON strings

## Best Practices Summary

1. **Match the data model to the database strengths**
2. **Use consistent primary key strategies**
3. **Plan for data evolution and growth**
4. **Consider operational complexity**
5. **Design for your access patterns**
6. **Maintain data type consistency across systems**
7. **Document your modelling decisions and rationale**

---

**Navigation:**

- Previous: [Data modelling and distributed file storage](./data-modelling-distributed-file-storage.md)
- Next: [Indexing](./indexing.md)
