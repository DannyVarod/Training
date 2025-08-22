# Data modelling and messages

## Data structure

Prefer a single message structure per queue/topic. Avoid basing messages on classes with inheritance.

Do NOT remodel inheritance as composition.

If you do have inheritance, model the messages the same as in the table-per-hierarchy strategy [see data modelling and relational databases](./data-modelling-relational-dbs.md)

If you have some variance in structure, use a flexible type like JSON, not Avro as serialization type.

## Data serialization type and compression

In messages, it is important to take the message structure and the technologies that may be used into account when choosing how to serialize data.

Polymorphism should be avoided where possible.

If you do need to use polymorphism, then ONLY serialize your data using flexible standards, such as JSON, do NOT use Avro.

If you might need to use multiple languages for reading the data (and at some point **you probably will**), using JSON is a big advantage and using Avro is a big disadvantage, as support for features in Avro differs between programming languages.

For large message sizes or topics with a large size (`retention * message size * throughput`) enable compression, select a compression type that has a good size/speed ratio dependent on your purposes.

### Message ordering

For **message queues** (e.g. RabbitMQ), the order is guaranteed, as long as processing of one message does not fail while a following message has already started processing.

For **message topics** (e.g. Kafka), take into account that each message is published with a (non-unique) key which is used for partitioning. You can grantee ordering of messages by giving them the same key e.g. for garanteeing order of events per account use the account_id as the partitioning key, whereas if you do not care about the order, use a random GUID as the key to improve balance between partitions.

### Number of partitions

Consider the required throughput, multiply by a factor of expected growth in mid-range future, multiply by processing duration of a single message, make sure units match, round up, add a buffer.

For example:

```text
10M messages per hour (current **peek** throughput) * 5 (expected growth in 2 years) * 10 mSec * 1/3600/1000 (convert milliseconds to hours)
= 50M/3.6M
~= 14 (round up) consumer instances,
Add a buffer ==> 20 partitions for this topic.
```

---

**Navigation:**

- Previous: [Data modelling and columnar databases](./data-modelling-columnar-dbs.md)
- Next: [Data modelling and distributed file storage](./data-modelling-distributed-file-storage.md)
