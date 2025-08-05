# Data modelling intro - data types

[Previous: Data modelling and database types](./data-modelling-db-types.md) | [Next: Data modelling and relational databases](./data-modelling-relational-dbs.md)

## Overview

This section introduces the fundamental concepts of data types and their role in data modelling across different database systems.

## Basic Data Types

### Primitive Types

- **Numbers**: Integers, floating-point numbers, decimals
- **Booleans**: True/false values
- **Strings**: Text data, character sequences
- **Dates and Timestamps**: Temporal data types
- **GUIDs** (or **UUIDs**): Globally unique identifiers

### Complex Types

- **Arrays** (or **Lists**): Ordered collections of elements
- **Objects** (or **Dictionaries**): Key-value structures
- **Binary**: Raw binary data, files, images
- **JSON** or **XML**: any of the above data types, serialized using a standard protocol into text data

## Data types and CPU efficiency

### Natively supported data types

CPUs (Central Processing Units) and GPUs (Graphical Processing Units) work the most efficiently with types they support natively, the most common natively supported these types on CPUs and GPUs are:

- **Byte** or **Char**: an 8-bit (1 Byte) whole number which can be unsigned (range 0...255) or signed (range -128...127) and can also be used to represent a character single or language selection in a string or a part of a character, depending on the text encoding (languages) of the string e.g. for UTF-8 or ASCII

- **Short**: a 16-bit (2 Byte) whole number which can be unsigned (range 0...FILL) or signed (range -FILL...FILL), this also be used to represent a character or language selection in a multilingual string, depending on the text encoding (languages) of the string e.g. for UTF-16

- **Int**: a 32-bit (4 Byte) whole number which can be unsigned (range 0...FILL) or signed (range -FILL...FILL), this also be used to represent a character in a multilingual, depending on the text encoding (languages) of the string e.g. for UTF-32

- **Long**: a 64-bit (8 Byte) whole number which can be unsigned (range 0...FILL) or signed (range -FILL...FILL)

- **Float** (or single precision floating point number): a 32-bit (4 Byte) floating point number where 1 bit represents the sign +/-, 23 bits represent a whole number (called the fraction) and 8 bits represent the exponent e.g. - 1001 exponent -3 ==> -1001e-3 = -1.001 or + 7 exponent +5 = 700000. 32-bit floats are smaller and faster for inaccurate calculations, however, due to their limited resolution, the numbers you store will not be accurate e.g. instead of 1.0 you may get a number like 0.999999999999999 or 1.00000000000001.

- **Double** (or double precision floating point number): a 64-bit (8 Byte) floating point number where 1 bit represents the sign +/-, 52 bits represent a whole number (called the fraction) and 11 bits represent the exponent. While this is much more accurate than using a 32bit float, this is still not accurate enough for accounting

### Other primitive data types

- **Decimal**: a flexible data type in which the precision and scale are defined in decimal digits, not binary digits e.g. decimal(20, 6) = 20 decimal digits (precision), 6 of which after the point (scale) e.g. +/- 12345678901234.123456, this enables a set accuracy for accounting, however, the number of bytes each number takes depends on the defined precision. Some databases e.g. AWS DocumentDB do not support this

- **GUID** aka **UUID**: a 128bit (16 Byte) type which is usually printed in hexadecimal (base-16) e.g. "eb58d040-5edc-45ed-9287-ae90eecdd8a0". Some databases support storing GUIDs efficiently (as 16 Bytes) e.g. SQL Server, MongoDB, Delta, while others store GUIDs as a 36 Character string (36 Bytes) e.g. BigQuery, Redshift, Snowflake. GUIDs are created randomly, using the ID of the machine and the timestamp of the creation to help with uniqueness and with a range of 340,282,366,920,938,463,463,374,607,431,768,211,456 (3.5e38) collisions are very unlikely.

### Temporal data types

Databases usually support these temporal data types:

- **Date**: a date (without time), e.g. "2025-08-07", best printed in ISO 8691 (International Organization for Standardization, standard number 8691), so that the date parts are sorted from most significant to least significant, which enables sorting strings with dates in them resulting in sorting by date.

- **Timestamp***: a date and time, usually stored as a number e.g. number of milliseconds before or after 1970-01-01. Timestamps have a resolution e.g. seconds, milliseconds, microseconds and CAN have an offset (timezone where timestamp was created vs. UTC), e.g. "2025-08-07T12:17:00.000+01:00" or "2025-08-07T12:17:00.000Z" (Z = offset of 00:00). Some databases support storing timestamps with or without offsets, while in others you'll have to normalize the timestamps to UTC in order to prevent confusion. Remember that the offset vs. UTC of the country you live in changes twice a year (due to day light savings), so if you use this in a timestamp without offset, the timestamps in your database will jump back and forward an hour when day light saving starts and stops.

### Strings (text)

Many databases support these data types:

- **char(N)**: an 8-bit character string with a length of exactly N characters (N characters)
- **nchar(N)**: a 16-bit character string with a length of exactly N characters (N characters)
- **varchar(N)**: an 8-bit character string with a maximum length of N characters (0...N characters)
- **nvarchar(N)**: a 16-bit character string with a maximum length of N characters (0...N characters)

For varchar/nvarchar, database stores the data + the number of characters or a string end token, so the storage space is a bit larger than char/nchar.

nchar/nvarchar take up about twice as many Bytes as char/varchar.

Database indices can be limited to a number of bytes, which leads to a limit of how many and which columns you can use as an index, for instance in SQL Server, indices are limited to about 900 Bytes, so if they are to include one or more string columns, these columns must be limited in length.

## Selecting data types

Best practices for selecting data types:

- In relational databases, limit the data type sizes where possible, yet don't over do it e.g. don't assume names are less than 20 characters and don't assume names can be stored without encoding (do not use varchar for people's names or addresses)

- Where possible, store IDs as GUIDs (UUIDs), not as strings

- Where possible, store timestamps with offsets, if not then convert timestamps to UTC prior to storing

- Use decimal, not double (or float) for financial amounts, unless they are only used for machine learning, where the approximate amount is good enough

## Naming conventions and data types

Do NOT use the hungarian notation for column/field/variable names - never prefix names with type e.g. uItem, bCorrect, iItems, dCreate, tsUpdate are NOT good names, names should be clear enough to understand both meaning of column and data type e.g. itemId, isCorrect, numItems, createdAt, updatedAt. If you come across a case where the type is still not clear, then suffix the name with type, do NOT prefix it e.g. createdDate vs. createdTs is OK, dCreated, tsCreated is NOT OK.

---

[Previous: Data modelling and database types](./data-modelling-db-types.md) | [Next: Data modelling and relational databases](./data-modelling-relational-dbs.md)
