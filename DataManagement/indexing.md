# Indexing

Indices enable fast searching of data in databases, they are used in both relational databases and document databases.

Without indices, the database has to perform a scan - a search row by row / document by document for results, with a complexity of O(N), where N is the number of rows/documents in the table/collection.

With indices, the database can perform a seek - search within a tree-structure for finding results, with a complexity of O(log(N)).

Primary keys are indexed, enabling searching for records/documents using their ID.

An index can contain one column/field or more.

The more indices you define (and more columns you put in a index), the larger the storage required for these indices and the higher the latency becomes, so don't index everything blindly.

Rules for creating indices:

1. Look at the queries you run, from most common to least common:
    1. For each of these queries, which columns/fields do they use in their filter (where clause)?
        1. For each of these columns, how well do the values divide the data - for instance `boolean_value = true` only filters-out half the values, `date_value = '2020-01-01'` filters-out most of the data (assuming the data isn't all from one day). If the column is effective in filtering out the data, include it in your index.
        2. Sort the columns/fields you need for the query from the one with the highest filtering to the one with the lowest filtering, this usually is the optimal order for the fields in you index. Take into account that if you use a field for equality (`=`) it filters out much more data than if you use it as a range (`>=`, '`>`, `<=`, `<`).
    2. Did you reach similar indices for 2 different queries e.g. `index1: col1, col2, col3, col4`, `index2: col1, col3, col4` where index1 is index2 with additional columns? - If so you can discard index2, because searching for `col1=A, col2=<anything>, col3=C, col4=D` is the same as searching for `col1=A, col2=B, col3=D`.
    3. Which columns/fields do they use in their project (select clause)? - In some relational database types you can add "include columns" to the end of you index, these are columns the database can use to return a result directly from the index, without actually reading the row. This means that if you often use an index to look at a few specific columns in the table, the database can save time if you include these specific columns in the index

If you have a long running query, profile/insect it to see how it searches for the data - does it use seek or scan? See if the fields it uses are indexed and if creating a composite index according to the above rules could help it find the data by filtering-out rows/documents faster.

---

**Navigation:**

- Previous: [Data modelling summary](./data-modelling-summary.md)
- Next: [Concurrency and databases](./concurrency-and-dbs.md)
