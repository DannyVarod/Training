# Database types summary

This section provides a comprehensive comparison of different database and storage technologies to help you choose the right tool for your specific use case.

## Comparison Table

| Database Type | Common Examples | Best For | Advantages | Disadvantages | When to Use | When NOT to Use |
|---------------|-----------------|----------|------------|---------------|-------------|-----------------|
| **Relational** | SQL Server, PostgreSQL, MySQL | Structured data with ACID requirements | ACID compliance, SQL standard, data validation, transactions | High latency, low throughput, complex joins for nested data | Authentication, financial data, structured business data | Nested data, high-scale analytics, frequent schema changes |
| **Document** | MongoDB, AWS DocumentDB, CosmosDB | Semi-structured data, rapid development | Low latency, flexible schema, handles nested data well | Limited concurrency, eventual consistency, complex aggregations | Content management, catalogs, user profiles | Critical financial data, complex transactions, real-time consistency |
| **Key-Value** | Redis, DynamoDB, ElastiCache | Simple data with fast access | Very low latency, simple operations, easy scaling | Limited queries, no relationships, simple data structures | Caching, session storage, counters, pub-sub | Complex relationships, reporting, nested data |
| **Wide-Column** | Cassandra, HBase, BigTable | Large-scale distributed data | Massive scale, partition tolerance, column-family model | Query only by key, limited data structures, operational complexity | Time-series data, IoT data, large distributed datasets | Small datasets, complex queries, ACID transactions |
| **Columnar** | Snowflake, BigQuery, Redshift | Analytics and data warehousing | Fast aggregations, compression, analytics-optimized | Slow for OLTP, expensive updates, complex operational setup | Business intelligence, reporting, data warehousing | Real-time transactions, frequent updates, operational systems |
| **Message Queues** | RabbitMQ, SQS, Service Bus | Point-to-point messaging | Reliable delivery, load balancing, dead letter handling | No replay, single consumer per message, no history | Task processing, job queues, reliable messaging | Event streaming, multiple consumers, message replay |
| **Message Topics** | Kafka, MSK, Event Hubs | Event streaming and pub-sub | Multiple consumers, replay capability, high throughput | Complex setup, no cross-partition ordering, operational overhead | Event sourcing, real-time analytics, log aggregation | Simple messaging, low latency responses, small scale |
| **Distributed File Storage** | S3, HDFS, Azure Blob | Large file and binary storage | Massive scale, cost-effective, durability | High latency, eventual consistency, no queries | Backup, media storage, data lakes, archiving | Frequent updates, low latency access, structured queries |

## Decision Matrix

### By Use Case

| Use Case | Recommended Technologies | Why |
|----------|--------------------------|-----|
| **Web Application Backend** | Relational + Key-Value (cache) | ACID for business logic, fast cache for sessions |
| **Content Management** | Document + Distributed File Storage | Flexible schema for content, files for media |
| **Analytics & Reporting** | Columnar + Distributed File Storage | Optimized for aggregations and large datasets |
| **Real-time Streaming** | Message Topics + Key-Value | Event processing with fast state management |
| **Microservices Communication** | Message Queues or Message Topics | Reliable async communication with event broadcasting |
| **IoT Data Platform** | Wide-Column + Message Topics | Handle massive scale with real-time ingestion |
| **E-commerce Platform** | Relational + Document + Key-Value | Transactions, catalog flexibility, fast lookups |

### By Scale Requirements

| Scale | Recommended Approach | Technologies |
|-------|---------------------|-------------|
| **Small Scale** (< 1M records) | Single database with caching | Relational + Key-Value |
| **Medium Scale** (1M - 100M records) | Specialized databases per use case | Document/Relational + Message Queues |
| **Large Scale** (100M+ records) | Distributed architecture | Wide-Column/Columnar + Message Topics + Distributed Storage |
| **Massive Scale** (Billions+ records) | Purpose-built distributed systems | Columnar + Message Topics + Distributed File Storage |

### By Latency Requirements

| Latency Requirement | Recommended Technologies |
|---------------------|-------------------------|
| **Ultra-low** (< 1ms) | Key-Value (in-memory) |
| **Low** (1-10ms) | Document, Key-Value |
| **Medium** (10-100ms) | Relational, Wide-Column |
| **High** (100ms+) | Columnar, Distributed File Storage |

## Architecture Patterns

### Lambda Architecture

- **Batch Layer**: Columnar + Distributed File Storage
- **Speed Layer**: Message Topics + Key-Value
- **Serving Layer**: Document or Wide-Column

### Microservices Architecture

- **Per-service databases**: Match database type to service needs
- **Communication**: Message Queues or Message Topics
- **Shared cache**: Key-Value
- **Analytics**: Columnar with data pipeline

### Event Sourcing

- **Event Store**: Message Topics (Kafka)
- **Read Models**: Document or Relational
- **Snapshots**: Key-Value or Document
- **Analytics**: Columnar

## Common Developer Mistakes

### 1. "NoSQL is Always Faster"
**Misconception**: MongoDB/DocumentDB will always outperform SQL Server
**Reality**: For simple CRUD on structured data, relational databases often perform better
**Example**: User authentication with 3 fields vs. nested user profiles with arrays
**Fix**: Choose based on data structure complexity, not assumed performance

### 2. "One Database Per Application"
**Misconception**: Each application should use only one database technology
**Reality**: Most successful applications use multiple databases for different purposes
**Example**: E-commerce using SQL for orders + Redis for cart + S3 for images
**Fix**: Use the right tool for each specific use case within your application

### 3. "Scaling Means Going NoSQL"
**Misconception**: When you need to scale, switch from SQL to NoSQL
**Reality**: Relational databases can handle millions of records with proper design
**Example**: Instagram used PostgreSQL for 100M+ users before considering alternatives
**Fix**: Optimize current solution first, then consider alternatives if truly needed

### 4. "Eventual Consistency is Always Acceptable"
**Misconception**: Applications can always handle eventual consistency
**Reality**: Some business logic requires immediate consistency
**Example**: Financial transactions, inventory management, user authentication
**Fix**: Identify which parts of your system require strong vs. eventual consistency

### 5. "More Technology = Better Architecture"
**Misconception**: Using many different databases shows sophisticated architecture
**Reality**: Each additional technology increases operational complexity exponentially
**Example**: Team managing PostgreSQL + MongoDB + Redis + Kafka + Elasticsearch
**Fix**: Start simple, add complexity only when clearly needed and justified

### 6. "Schema-less Means No Data Modeling"
**Misconception**: Document databases don't require thinking about data structure
**Reality**: You still need to design for your access patterns and relationships
**Example**: Storing normalized data in MongoDB (defeats the purpose)
**Fix**: Design documents around how you'll query and update the data

### 7. "Analytics Databases for Operational Workloads"
**Misconception**: Columnar databases like BigQuery can replace operational databases
**Reality**: They're optimized for read-heavy analytics, not frequent updates
**Example**: Using Snowflake for real-time user session management
**Fix**: Use columnar for reporting/analytics, operational databases for live systems

## Key Takeaways

1. **No single database fits all use cases** - Use multiple technologies together
2. **Start simple** - Begin with relational + cache, evolve as needed
3. **Consider operational complexity** - Distributed systems require more expertise
4. **Plan for scale** - Design data architecture for expected growth
5. **Evaluate consistency needs** - Choose between strong and eventual consistency carefully
6. **Monitor and measure** - Performance characteristics vary significantly between technologies
7. **Question assumptions** - Performance and scalability depend on specific use cases
8. **Design for your team** - Consider operational capabilities and expertise

---

**Navigation:**

- Previous: [Distributed File Storage](./distributed-file-storage.md)
- Next: [Data modelling and database types](./data-modelling-db-types.md)
