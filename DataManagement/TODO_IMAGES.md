# TODO: Potential Image Opportunities

This document lists text concepts from database-related markdown files that could be illustrated with images to enhance understanding.

## **Document Databases (document-dbs.md)**

### 1. Sharding Concept
**Text**: "Document Databases can have sharding to divide data across multiple instances by key"
**Image Idea**: Show data partitioning across multiple database instances, with documents distributed by shard key (e.g., user_id ranges)
No: I will explain this using existing image

### 2. Replica Sets
**Text**: "Document Databases can have replica sets which enable both redundancy of data and higher throughput of reads"
**Image Idea**: Show primary/secondary replica architecture with read/write flow arrows indicating writes go to primary, reads can come from secondaries
No: I will explain this using existing image

### 3. Nested JSON Indexing
**Text**: Complex nested document examples and indexing on sub-fields like "more_info.hobbies"
**Image Idea**: Visual representation of how MongoDB creates indexes on nested fields, showing document structure and index tree
No: there is a separate chapter on indexing, which I will add images to later

### 4. Aggregation Pipeline
**Text**: Multi-stage aggregation process ($match → $group → $project)
**Image Idea**: Visual flowchart showing data transformation through each pipeline stage with sample data
Yes: add image

## **Key-Value Databases (key-value-dbs.md)**

### 1. In-Memory Storage
**Text**: "These databases usually store the entirety of the data in-memory"
**Image Idea**: Memory vs disk storage comparison showing speed differences and capacity limitations
No

### 2. Redis Data Structures
**Text**: Various data types (string, list, set, hashset, sorted list, geo-coordinates) and their operations
**Image Idea**: Visual representation of each Redis data structure with sample operations and use cases
No: existing 2nd image will be enough to illustrate as I talk

### 3. Key Naming Conventions
**Text**: Hierarchical key structure using ":" separators (my_app:object_type:object_key)
**Image Idea**: Tree-like visualization showing how colon-separated keys create logical hierarchies
No

### 4. TTL (Time To Live)
**Text**: "Built-in data expiration (TTL)"
**Image Idea**: Timeline showing how data expires automatically, with keys disappearing after expiration time
Update: add another (key, string value) to existing image with keys values and include a TTL for the new key

## **Wide-Column Databases (wide-column-dbs.md)**

### 1. Tablet Concept
**Text**: "Tables are created with a defined set of tablets" and "Per tablet, each key can contain any set of columns"
**Image Idea**: Show how tablets organize data within a table, with different keys having different column sets
No: existing 2nd image already shows this

### 2. Key Range Scanning
**Text**: "you can scan data in-order of key values from one key to another"
**Image Idea**: Visualize scanning between key ranges, showing ordered traversal through sorted keys
No: too detailed

### 3. Data Partitioning
**Text**: Distributing large datasets across multiple nodes
**Image Idea**: Show how data is partitioned across nodes by key ranges for horizontal scaling
Yes: add image

### 4. Time-Series Patterns
**Text**: Time-series data storage with device_id + timestamp keys
**Image Idea**: Illustrate typical IoT data storage patterns with temporal key organization
Update: the image with data partitioning should illustrate this too

## **Message Queues (message-queues.md)**

### 1. Message Consumption
**Text**: "Once a message is consumed, it is typically removed from the queue"
**Image Idea**: Before/after states showing messages being consumed and removed from queue
Yes: add image

### 2. Dead Letter Queues
**Text**: Dead letter handling for failed messages
**Image Idea**: Flow diagram showing failed message processing and routing to dead letter queue
No: too detailed

### 3. Message Acknowledgment Flow
**Text**: Message acknowledgment and reliable delivery
**Image Idea**: Sequence diagram showing producer→queue→consumer→acknowledgment cycle
Update: edit 2nd image (Queue: "orders") to illustrate this on the existing data in the image

### 4. Priority Queues
**Text**: Priority queue support
**Image Idea**: Queue visualization showing how high-priority messages jump ahead in processing order
Yes: however, do they "jump" ahead or are there a stack of queues by priority within the queue?

## **Vector Databases (vector-dbs.md)**

### 1. Embedding Process
**Text**: "embeddings from large language models (LLMs) such as models from HuggingFace, OpenAI, etc."
**Image Idea**: Show text/image → ML model → vector transformation process with actual dimensional examples
Yes: add new image before existing image

### 2. Distance Metrics
**Text**: "uses distance metrics (like cosine similarity) to find the closest vectors"
**Image Idea**: Visual comparison of cosine similarity vs euclidean distance in vector space with geometric representation
No: too detailed

### 3. Vector vs Keyword Search
**Text**: Semantic meaning vs keyword-based search differences
**Image Idea**: Side-by-side comparison showing different search results for same query using semantic vs keyword approaches
No: I'll mention this in talk

### 4. Similarity Search Process
**Text**: Finding similar vectors in high-dimensional space
**Image Idea**: 2D/3D representation of vector space showing query vector finding nearest neighbors
No: too detailed

## **Relational Databases (relational-dbs.md)**

### 1. JOIN Operations
**Text**: Various SQL JOIN types mentioned in examples
**Image Idea**: Visual representation of INNER JOIN, LEFT JOIN, RIGHT JOIN, FULL OUTER JOIN with sample tables
No: I will be skipping over this part in talk

### 2. Indexing Concepts
**Text**: Primary keys and indexing for fast lookups
**Image Idea**: B-tree or hash index structure showing how indexes speed up queries
No: there is a separate chapter on indexing

### 3. Normalization
**Text**: Structured data with foreign key relationships
**Image Idea**: Illustrate 1NF, 2NF, 3NF with before/after table structures showing normalization benefits
No: there is a separate chapter on data modelling

## **Columnar Databases (columnar-dbs.md)**

### 1. Row vs Column Storage
**Text**: "Data is stored column-wise, meaning all values for a single column are stored together"
**Image Idea**: Side-by-side comparison showing how same data is stored in row-based vs column-based format
No: this is what the arrows in the existing images are for

### 2. Compression Benefits
**Text**: "High compression ratios (similar values compress well)"
**Image Idea**: Show how similar values in columns compress better than mixed values in rows
No: too detailed

### 3. Nested Data Structures
**Text**: Complex nested SQL structures with arrays and structs
**Image Idea**: Illustrate how nested data (arrays, structs) is stored and queried in columnar format
No: too detailed

## **Message Topics (message-topics.md)**

### 1. Consumer Groups and Offsets
**Text**: "Each type of consumer has a 'bookmark' called an offset per partition"
**Image Idea**: Show how multiple consumer instances share partitions and track individual offsets
No: existing image already illustrates this

### 2. Partition Rebalancing
**Text**: Consumer group management and partition assignment
**Image Idea**: Illustrate what happens when consumers join/leave a group and partitions are reassigned
No: too detailed

### 3. Message Retention vs Compaction
**Text**: "Messages are kept for a configurable time period"
**Image Idea**: Timeline showing different retention strategies (time-based, size-based, compaction) over time
No: too detailed

## **Distributed File Storage (distributed-file-storage.md)**

### 1. Data Replication
**Text**: "Data is replicated across multiple nodes"
**Image Idea**: Show how files are replicated across multiple DataNodes for fault tolerance with replication factor
No: existing image already illustrates this

### 2. Consistency Models
**Text**: "Eventually consistent in many cases"
**Image Idea**: Illustrate eventual consistency vs strong consistency scenarios with timeline of data propagation
No: too detailed

### 3. File Format Optimization
**Text**: "different file formats for different use cases such as parquet files"
**Image Idea**: Show different file formats (Parquet, ORC, Avro) and their structural differences and use cases
No: too detailed

---

## Priority Recommendations

See answers above, do NOT use the below priorities

**High Priority** (would significantly improve understanding):
1. Vector database embedding and similarity search process
2. Document database sharding and replica sets
3. Row vs column storage comparison
4. Message queue vs topic consumption patterns
5. Redis data structures overview

**Medium Priority** (nice to have):
1. JOIN operation visualizations
2. Key-value TTL and expiration
3. Wide-column tablet organization
4. ACID transaction rollback (already completed)

**Low Priority** (supplementary):
1. File format comparisons
2. Consistency model timelines
3. Normalization examples

## MORE TODOs (fixes)

* in relational dbs images arrow, acid1, acid2 the empty space at bottom of images is too large, it should be reduces to equal empty space at top of image (per image)

* in message queues layout image the new ack arrow is overlapping the dequeue arrow
* in message queues the before/after image is redundant delete file and remove reference
* in message queues priorities image the label "2nd" need to be moved up a little because it is behind an arrow and the queues need more space at the bottom
* in document dbs the reference to the new aggregation image is broken
* in document dbs aggregation image there is an error "error on line 71 at column 131: xmlParseEntityRef: no name"
* in document dbs aggregation image the width of the elements is too small for content
* in document dbs aggregation image, aggregating by age >= 25 makes no sense, aggregating by age/10 would make much more sense
* in document dbs aggregation image, I could not see the $project content due to the above error
