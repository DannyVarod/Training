# Wide-column databases

Wide-column Databases, one of the various types of NoSQL databases, often have the following structure:

- Database
    - Table
        - Tablet / Column families
            - Key

Common examples include BigTable, HBase and Cassandra.

Tables are created with a defined set of tablets.

Per tablet, each key can contain any set of columns, similar to a shallow (one-level) document.

In HBase / BigData, the values in columns are stored as binary, so the client code needs to serialize / deserialize values to / from binary.

Searching is by keys only, however, you can scan data in-order of key values from one key to another.

You can choose which tablets of the table to read/write from/to in order to perform projection.

This structure enables low latency (with key seek only) together with distributing / partitioning of large data.

HBase and BigTable do not have a query language or a UI for querying, however, they have the same API which you can access for a large variety of programming languages.

Advantages:

- Very low latency and high throughput
- Supports an extremely large amount of data
- Can scale up and down on demand (BigTable only)
- Very useful for key-value access for data too large for Redis

Disadvantages:

- Query only by key
- Limited data structures supported i.e. only flat documents
- Tablets per table cannot be changed after table creation

When to use:

- Flat data only accessed by key
- Low latency is required
- Large amount of data
- Time-series data (IoT sensors, logs, metrics)
- Need to scale beyond what Redis can handle in memory

When NOT to use:

- Small amount of data
- Nested data
- For reports, aggregations, statistics, machine learning
- Complex query requirements
- Need for ACID transactions across multiple rows

## Real-World Use Cases

### Netflix's Viewing History
Netflix uses Cassandra to store viewing history for 200M+ subscribers, handling billions of writes per day with low latency requirements.

### Apple's iMessage
Apple uses Cassandra for iMessage storage, requiring massive scale and fast lookups by user ID for message delivery.

### Time-Series Data
Companies like Tesla use wide-column databases for storing IoT sensor data from vehicles, where keys are device_id + timestamp.

Signs you are misusing:

- JSON or XML data stored in column and you need to be able to search inside them

---

**Navigation:**

- Previous: [Key-Value databases](./key-value-dbs.md)
- Next: [Columnar databases](./columnar-dbs.md)
