# Data modelling and document databases

## Denormalizing data

For Document Databases denormalizing the data into flat data WITH duplicated fields is the best practice. Do NOT split documents into multiple collections.

**Rationale**:

- Lower latency - Selecting from a single collection instead of using IDs in results to get data from other collections speeds up queries and also insertion of data.
- Duplicate values - Document databases often do not support joins and even if they do, it is not supported in simple queries.
- Nested - Document databases support nested objects and arrays and using these in indices, take advantage of this
- Number of fields in document is not an issue - Since Document databases enable projection, when reading you can select with fields to fetch from the database to reduce IO bandwidth.

For example:

**students.Students**:

```JSON
{
    "student_id": "e307068f-6405-4190-8298-a3f9003f0f8f",
    "first_name": "John",
    "last_name": "Smith",
    "birthdate": "2000-01-01",
    "created_at": "2025-08-01T12:00:00.000",
    "updated_at": "2025-08-07T22:00:00",
    "email_addresses": [
        "john.smith.372@somecollege.edu",
        "john.smithy.y2k@gmail.com"
    ],
    "vehicles": [
        {"vehicle_license_plate": 111111111, "manufacturer": "Honda"},
        {"vehicle_license_plate": 222222222, "manufacturer": "Volkswagen"}
    ]
}
```

Note that created_at and updated_at were normalized to UTC, since offset is not supported in this example (depends on specific database type).

Prefer storing table with sensitive data in a relational database and not in same database as the rest of the data.

Do NOT use database generated GUIDs/UUIDs.

## Choosing primary keys and Naming columns

See rules for choosing primary keys and naming columns in [Data Modelling and Relational Database](./data-modelling-relational-dbs.md) as these rules apply to all database types.

## Different object types

While Document database do enable storing any documents with any structure together in the same collection, actually doing so is a bad practice. A good practice is one object type (class) / data schema / message schema per collection (not including sub-documents).

Polymorphism is OK, you can store similar classes in the same collection if they are used in the same way, however, do not over do this. If you only have one collection for all your object types, you are very likely misusing the database.

**See also:**

- [Indexing](./indexing.md)

---

**Navigation:**

- Previous page: [Data modelling and relational databases](./data-modelling-relational-dbs.md)
- Next page: [Data modelling and key-value databases](./data-modelling-key-values-dbs.md)
