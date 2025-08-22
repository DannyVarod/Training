# Relational databases

Relational databases, or classic databases are databases comprised of the following structure:

- Instance (the server or VM you installed the database engine on or instance of a SaaS you created)
    - Database (some database vendors call these Schemas instead e.g. Oracle)
        - Schema (some databases vendors do not have this extra level e.g. in MySQL a database = a schema)
            - Table (see below)
                - Row (the data you store)

Common examples include SQL Server (aka MS-SQL), Postgres, MySQL and more.

A table is a structure in which you store data with the same structure e.g.

ID | Name | Age | Studying | Updated
--- | --- | --- | --- | ---
3489347 | Alice | 20 | Software Engineering | 2025-02-20
1347347 | Bob | 21 | Data Science | 2025-05-10
8945784 | Clair | 19 | null | 2025-08-01

Each table has a definition which includes what fields are meant to be in the data.

Each field definition is called a column and consists of a name for the column, a data type and whether the values of the fields for this column have to be defined or if they can be null (undefined).

Each item in the table is called a row, which has a value per column (or null if value is undefined).
Tables use keys and indices (indexes) to enable searching quickly within large datasets.

Data is stored row-by-row, so with a row transversal is fast due to data locality.

The language used to define the structures and to query the database is called SQL (Structured Query Language).

Tables are created with a definition.
(also known as the schema of the table, not to be confused with the logical schema in databases that contains tables)

Most relational database enable modifying the definition of existing tables, however, if the table is already populated with a large amount of data, this may cause a delay in running operations until modification completes.

Example:

```SQL
create table db_name.schema_name.table_name (
    ID bigint not null primary key,
    Name nvarchar(200) not null,
    Age smallint null,
    Studying nvarchar(500) null,
    Updated date null
)
```

For column names with non-alpha-numeric characters, the name needs to be wrapped like so `"Long Column Name"` in most databases or like `[Long Column Name]` in SQL Server (ms-sql).

This also enables using names that are reserved for type names e.g. `int` or functions e.g. `len`, however, using these names is not recommended due to confusing this can cause and due to the mistakes it can cause if you forget to wrap the name in some usage.

`null` declares that the column accepts undefined values, `not null` declares that the column does not accept undefined values.

`primary key` or `unique` define a column as one that does not allow duplicate values and `primary key` also defines a column as the default for searching and in some cases also determines the storage order of the table.

You can insert one or more rows for example:

```SQL
insert into db_name.schema_name.table_name
(
    ID,
    Name,
    Age,
    Studying,
    Updated
) values
(3489347, 'Alice', 20, 'Software Engineering', '2025-02-20'),
(1347347, 'Bob', 21, 'Data Science', '2025-05-10'),
(8945784, 'Clair', 19, null, '2025-08-01')
```

You can find data for example:

```SQL
select
    * -- all columns
from db_name.schema_name.table_name
-- warning, this will return all rows
```

Filter the results:

```SQL
select
    * -- all columns
from db_name.schema_name.table_name
where Age >= 20
```

Use projection to reduce data bandwidth by selecting only the columns you need

```SQL
select
    Name,
    Age
from db_name.schema_name.table_name
where Name like '%i%' -- names containing an "i"
```

You can modify existing rows:

```SQL
update db_name.schema_name.table_name
set
    Age = Age + 1,
    Updated = getdate()
where Updated < getdate()
```

Upserting:

```SQL
merge db_name.schema_name.table_name as tgt
using (
    values
    (3489347, 'Alice', 20, 'Software Engineering', '2025-02-20'),
    (1347347, 'Bob', 21, 'Data Science', '2025-05-10'),
    (8945784, 'Clair', 19, null, '2025-08-01')
) as src (
    ID,
    Name,
    Age,
    Studying,
    Updated
)
on tgt.ID = src.ID
when not matched by target then
    insert (ID, Name, Age, Studying, Updated)
    values (src.ID, src.Name, src.Age, src.Studying, getdate())
when matched -- and any additional condition such as comparing dates
    then update set
        ID = src.ID,
        Name = src.Name,
        Age = src.Age,
        Studying = src.Studying,
        Updated = getdate()
```

Deleting:

```SQL
delete
from db_name.schema_name.table_name
where ID = 1347347
```

Add data from one table to another in a query:

```SQL
select
    s.Name as StudentName,
    c.Duration as CourseDuration
from college.data.Students s
inner join college.data.Course c
    on s.CourseID = c.CourseID
```

Aggregate data:

```SQL
select
    count(1) as number_of_students,
    min(Age) as min_age,
    max(Age) as max_age
from college.data.Students
```

Relational databases enable adding constraints to data e.g. not null, value of column must be a key in another table, running SQL code on insert / update of row.

Relational database are ACID compliant (Atomicity, Consistency, Isolation, and Durability), this means you can modify data in an "all or nothing" approach, prevent one operation from "breaking" the correctness of another etc.

Advantages:

- Simple query language which is more or less the same across multiple DB vendors
- Transactions enable complex data modification to be done concurrently while keeping data consistent
- Structure enforcement and data validations which can keep the data valid when you do not have a single data entry point that enforces data validity
- ACID compliance ensures data integrity
- Mature ecosystem with extensive tooling and expertise
- Strong consistency guarantees
- Standardized SQL across vendors

Disadvantages:

- High latency compared to NoSQL alternatives
- Low throughput for high-volume operations
- Nested data e.g. data with lists of items, data with sub-objects requires using additional tables (see [data modelling](./data-modelling-relational-dbs.md)) with foreign keys
- Reading and writing nested data requires multiple joins which can lead to very high latency
- "Crunching" large data is very slow, especially if cross-table
- Vertical scaling limitations
- Schema changes can be expensive on large tables

When to use:

- Authentication database
- Financial Balance database
- Data with flat and constant structure
- Data that requires consistent concurrent modifications to multiple items
- Systems requiring ACID transactions
- Applications needing strong consistency
- Traditional business applications (ERP, CRM)
- Audit and compliance systems

When NOT to use:

- Nested data with complex hierarchies
- Data with list of items, where fast loading all child items is required
- Data with frequent structure changes
- Binary data (use distributed file storage)
- Database mainly used for reports, aggregations, statistics, machine learning
- High-scale web applications requiring horizontal scaling
- Real-time analytics workloads

## Real-World Use Cases

### Stack Overflow's Core Data
Stack Overflow uses SQL Server for questions, answers, users, and votes - classic relational data with strong consistency requirements.

### Stripe's Payment Processing
Stripe uses PostgreSQL for payment transactions, requiring ACID compliance and strong consistency for financial data integrity.

### GitHub's User Management
GitHub uses MySQL for user accounts, authentication, and permissions - core operational data requiring reliability and consistency.

### Discord's User Relationships
Discord uses PostgreSQL for user accounts, friend relationships, and server memberships where referential integrity is crucial.

Signs you are misusing:

- JSON or XML or binary data stored in column
- Lots of joins in most queries (> 5 tables regularly)
- A large amount of code (stored procedures, functions) in database
- Huge amount of data and latency is not important for queries
- Using database as a message queue or cache
- Storing session data or temporary calculations

For best practices on using see [data modelling](./data-modelling-relational-dbs.md).

**See also:**

- [Data modelling and relational databases](./data-modelling-relational-dbs.md)
- [Indexing](./indexing.md)
- [Concurrency and relational databases](./concurrency-relational-dbs.md)

---

**Navigation:**

- Previous: [Database types (and alike)](./database-types.md)
- Next: [Document databases](./document-dbs.md)
