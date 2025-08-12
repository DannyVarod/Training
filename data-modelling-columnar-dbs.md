# Data modelling and columnar databases

## Denormalizing data

For Columnar Databases denormalizing the data into flat data WITH duplicated fields is the best practice. Do NOT split data into multiple tables.

**Rationale**:

- Less reads - Selecting from a table collection instead of using IDs in results to get data from other collections speeds up analytics.
- Storage size - Data is stored by-column, so columns can be compressed to reduce size, you do not need to count Bytes.
- Duplicate values - Joins slow down predicates (where clauses) for filters that are not on the first table's values, when using huge volumes of data this is critical.
- Nested - Columnar databases support nested objects and arrays and filtering based on their values, take advantage of this
- Number of fields in row is not an issue - Since Columnar databases store each column separately, when reading you can select with fields to fetch from the database to reduce IO bandwidth.

For example:

**students.Students**:

student_id | first_name | birthdate | created_at | updated_at | email_addresses | vehicles
--- | --- | --- | --- | --- | --- | ---
e307068f-6405-4190-8298-a3f9003f0f8f | John | Smith | 2000-01-01 | 2025-08-01T10:00:00.000+02:00 | 2025-08-07T19:00:00+03:00 | ["john.smith.372@somecollege.edu",  "john.smithy.y2k@gmail.com"] | [{"vehicle_license_plate": 111111111, "manufacturer": "Honda"}, {"vehicle_license_plate": 222222222, "manufacturer": "Volkswagen"}]

Prefer storing table with sensitive data in a relational database and not in same database as the rest of the data.

Columnar databases usually do not have database generated GUIDs/UUIDs or auto-increment IDs, if they do, then still do NOT use them.

## Variant / JSON column

Some columnar databases do not enforce a pre-defined struct for sub objects / array items (e.g. RedShift and Snowflake), do not abuse this for storing different object types / classes / message schemas in the same table under a variant / JSON column. Different types should be in different tables or columns (however, polymorphism within reason is OK).

## Choosing primary keys and Naming columns

See rules for choosing primary keys and naming columns in [Data Modelling and Relational Database](./data-modelling-relational-dbs.md) as these rules apply to all database types. Here in particular, using readable IDs for dictionaries is critical.

---

**Navigation:**

- Previous: [Data modelling and key-value databases](./data-modelling-key-values-dbs.md)
- Next: [Data modelling and messages](./data-modelling-messages.md)
