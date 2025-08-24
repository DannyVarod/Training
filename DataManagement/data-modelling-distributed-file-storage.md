# Data modelling and distributed file storage

When working with distributed file storage systems, data modelling focuses on how to organize, structure, and partition files to optimize for performance, scalability, and access patterns, since, unlike in databases, you have to do this yourself.

As explained in [data modelling and columnar databases](./data-modelling-columnar-dbs.md), denormalize the data, the same considerations apply to distributed file storage.

Consider using a management layer to abstract the storage as a data lake e.g. Databricks Delta.

## Key Considerations

### File Organization

- **Hierarchical structure**: Organize files in logical directory structures
- **Naming conventions**: Use consistent and meaningful file naming patterns
- **Partitioning strategies**: Partition data by time, geography, or other relevant dimensions

### Data Formats

- **Columnar formats**: Parquet, ORC for analytical workloads
- **Row-based formats**: Avro, JSON for transactional data
- **Compressed formats**: Reduce storage costs and improve transfer speeds
- **Schema evolution**: Choose formats that support schema changes over time

### Access Patterns

- **Read vs Write optimization**: Different strategies for read-heavy vs write-heavy workloads
- **Sequential vs Random access**: Optimize file sizes and organization accordingly
- **Batch vs Streaming**: Consider how data will be consumed

### Partitioning Strategies

- **Time-based partitioning**: Year/month/day/hour folders, this also enables easier implementation of hot-cold storage
- **Hash-based partitioning**: Distribute data evenly across storage nodes
- **Range-based partitioning**: Group related data together
- **Multi-dimensional partitioning**: Combine multiple partitioning strategies

## Best Practices

1. **Avoid small files**: Many small files can hurt performance
2. **Use appropriate compression**: Balance compression ratio vs processing speed
3. **Plan for data lifecycle**: Archive or delete old data appropriately
4. **Consider data locality**: Keep related data close together
5. **Monitor access patterns**: Adjust organization based on actual usage

## Common Patterns

### Data Lake Architecture

- Raw data in native formats
- Processed data in optimized formats
- Metadata management for discoverability

### Event Sourcing

- Immutable event logs
- Time-based partitioning
- Replay capabilities for rebuilding state

---

**Navigation:**

- Previous: [Data modelling and messages](./data-modelling-messages.md)
- Next: [Data modelling and vector databases](./data-modelling-vector-dbs.md)
