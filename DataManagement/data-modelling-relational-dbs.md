# Data modelling and relational databases

## Normalizing data

For Relational Databases (and **ONLY** for Relational Databases, NOT Columnar, NOT Wide-Column, NOT Document etc.) normalizing the data into flat data with no duplicated columns is the best practice.

**Rationale**:

- You can NOT efficiently store sub-objects in columns
- You can NOT efficiently store arrays in columns
- Data consistency, since foreign keys are used to keep data consistent, no need to duplicate values

For example:

**students.Students**:

student_id | first_name | last_name | birthdate | created_at | updated_at
--- | --- | --- | --- | --- | ---
e307068f-6405-4190-8298-a3f9003f0f8f | John | Smith | 2000-01-01 | 2025-08-01T10:00:00.000+02:00 | 2025-08-07T19:00:00+03:00

**students.EmailAddresses**:

email_address | student_id | created_at | updated_at
--- | --- | --- | --- | ---
john.smith.372@somecollege.edu | e307068f-6405-4190-8298-a3f9003f0f8f | 2025-08-01T10:00:00.000+02:00 | 2025-08-01T10:00:00.000+02:00
john.smithy.y2k@gmail.com | e307068f-6405-4190-8298-a3f9003f0f8f | 2025-08-07T19:00:00+03:00 | 2025-08-07T19:00:00+03:00

Here you see two tables, with a one-to-many relation between them, as a student can have multiple email addresses, however, two students sharing an email address is not permitted.

Despite having access to government ID numbers duration registration, this example uses GUIDs as the student_id, to prevent using private information in tables where it is not required. An additional table in a different, more access-limited, database would hold the mapping between student_id and country, goverment_id_type and goverment_id e.g.

**sensitive.StudentsGovernmentIds**:

student_id | country_iso_3166_1_a2 | goverment_id_type | goverment_id | created_at | updated_at
--- | --- | --- | --- | ---
e307068f-6405-4190-8298-a3f9003f0f8f | US | social_security | 12345678 |  2025-08-01T10:00:00.000+02:00 |  2025-08-01T10:00:00.000+02:00

This also enables multiple forms of identification per student.

Sometimes many-to-many relationships are required such as:

**students.StudentVehicles**:

student_id | vehicle_license_plate
--- | ---
e307068f-6405-4190-8298-a3f9003f0f8f | 111111111
e307068f-6405-4190-8298-a3f9003f0f8f | 222222222
4614afda-9f93-4ef9-bf32-76bba4403c82 | 222222222

**vehicles.VehicleDetails**:

vehicle_license_plate | manufacturer
--- | ---
111111111 | Honda
222222222 | Volkswagen

You may have notice some practices above that you are not used to regarding IDs...

## Selecting primary keys

The best practices for selecting primary keys may oppose many rules you were taught, however, these rules are based a lot of experience with legacy databases and new database, learning from issues caused by incorrect modelling, learning from abilities gained by correct modelling.

### Best practices for selecting primary keys

**Primary keys' structures**:

- ALWAYS define a primary key for tables

- NEVER define a primary key consisting of multiple columns, if you need multiple columns for identification, either concat them into one larger string e.g. "b04c7b69-32cb-4f35-aa6b-539f7452bb39/951c05e1-8140-4f32-9f24-5dab1ed99c13" or "US-CA-drivers-license-123456789" or generate a random ID for rows in this column EVEN if it is only used in this table e.g. "866d0e75-8ced-4e03-81a7-6ce270a70621". A unique compostie index is NOT a good replacement for primary keys.

- NEVER use database generated ID columns i.e. NO-auto-increment integers/longs, NO-database-generated-UUIDs

**Rationale**:

- Having one single primary key column enables easy software infrastructure for data processing, ORMs, ETLs and more e.g. automatic generation of merge scripts, automatic generation of history tables, automatic analysis of audit logs, automatic data consistency checking and much more.

- Using auto increment IDs mean that each environment you insert the same set of values into (e.g. for dictionary tables) will result in different IDs given for the same values

- Using auto increment IDs or database generated GUIDs/UUIDs means that if a bug, a concurrency issue, a network error etc. causes duplicate insertion of information, the database will not reject the duplicate resulting in loss of idempotency.

- Using auto increment IDs or database generated GUIDs/UUIDs means that using merge/upsert instead of insert to enter data safely with idempotency will not be possible.

Of course, there are hacks like turning auto increment on and off, however, if you are doing this then using auto increment was the wrong choice from the get go.

**Primary keys' data types**:

Tables are not enums, if you are using bytes/shorts/integers for dictionary tables you are over-normalizing the data, this results in  **Unreadable IDs** and **An extra join per-ID in each query** or using enums in code that require **code changes whenever a value is added** to table.

Instead, use short, length-limited, lower_snake or UPPER_SNAKE strings to identify dictionary values e.g.

**Countries**:

country_iso_3166_1_a2 | internet_tld | common_name | official_name
--- | --- | ---
US | us | United States | the United States of America
CA | ca | Canada | Canada
GB | uk | United Kingdom | the United Kingdom of Great Britain and Northern Ireland

In the above example, the primary key would be char(2) = 2 Bytes. "country_id" would be a good column name, however, in this case I chose a longer name which indicates which values to use per country, to encourage people to check the values adhere to the standard before adding them.

Longer IDs are OK too, for example:

**Hobbies**:

hobby_id | hobby
--- | ---
chess | Chess
rock_climbing | Rock climbing
rock_climbing_solo | Free soloing (Rock climbing)
poker | poker
poker_texas | Texas Hold'em (Poker)

Here the IDs are:

- limited in length and in Latin characters only, so you can use a small type like varchar(32) to represent them, which would only take up as much space as 2 GUIDs or 4 longs
- human readable
- more specific rows have the same prefix as the more general cases with a suffix that differs them

You could even abbreviate e.g. remove "ing" from word-ends e.g. rock_climb instead of rock_climbing, or poker_tx instead of poker_texus.

Whenever applicable use ISO standards for IDs.

## Additional Advantages of Application-Generated GUIDs

### Industry Endorsement: Netflix's Microservices Architecture
**Netflix Engineering** extensively uses application-generated GUIDs across their microservices platform, enabling:
- **Cross-service data consistency**: Same entity has same ID across all services
- **Simplified data migration between environments**: Dev/staging/prod maintain identical IDs
- **Better horizontal partitioning capabilities**: Use GUID prefixes to distribute data across shards

### Technical Benefits:
- **Horizontal Partitioning**: Distribute data across database shards using GUID prefixes
- **Offline Data Generation**: Create records without database connectivity (mobile apps, IoT devices)
- **Merge Conflict Resolution**: Identical records have identical IDs across all environments
- **Microservice Independence**: Services can generate IDs without coordination
- **Backup/Restore Simplicity**: No sequence/identity column complications during recovery

**See also:**

- [Indexing](./indexing.md)

---

**Navigation:**

- Previous: [Data modelling intro - data types](./data-types.md)
- Next: [Data modelling and document databases](./data-modelling-document-dbs.md)
