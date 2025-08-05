# Data modelling and key-value databases

Before you store data into a key-value database, consider what use cases the data needs to support.
Some use cases are good for simple key-value, some for key-hashset-value, some for key-list-value etc. and some and not suitable at all for a key-value database.

Read the database vendor's documentation to make sure you understand what it can do and how to use it before deciding if it can be used for your use case and how.

For example:

- Write a chunk of data, read back the chunk by key (e.g. for caching) - in this case storing the entire value as one JSON or binary chunk should be OK
- Write data, update some fields - in this case if the data is shallow, a hashset should be OK, if the data is not shallow, use a different database type
- Count events, use a long (int64) value, key should contain event type and any identifier you need e.g. key = `app_name:events:login:{user_id}:count`, call `INCR` per event
- Count events within a time window, use a list value with TTL on items, key should contain event type and any identifier you need and time bucket e.g. key = `app_name:throttling:login:global:1h:2132` for resolution of 1 minute, call `INCR` per event, then read keys for the bucket you need e.g. `app_name:throttling:login:global:1h:2033` ... `app_name:throttling:login:global:1h:2132` for the last 60 minutes, sum the results. Use `EXPIRE` and `INCR` to set TTL to 60 minutes.

---

**Navigation:**

- Previous: [Data modelling and document databases](./data-modelling-document-dbs.md)
- Next: [Data modelling and columnar databases](./data-modelling-columnar-dbs.md)
