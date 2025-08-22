# Testing

When test it is important to be aware of how databases work behind the scene and this can affect your tests.

The optimal way to test code depends on how the code works.

Code types:

## Logic only

Logic only - Some business logic, an algorithm, a unit of code that receives input and calculates the output without pulling data from an external system.

This code is optimal for **unit testing**. Mock internal logic-only units to isolate test to a single unit.

## Data access

Data access - When you have code that reads from a database, you can NOT mock the way the database behaves, **especially if you are using a relational database**.

Inserting into a table can be different that inserting into a list, due to unique constraints and other constraints defined on the table.

Changing a row in one table can effect queries on another table, due to joins and foreign keys.

This code is optimal for **component testing**.

### Steps for testing with data access code

- Create a short-living test database, either via a docker in the test/local machine, or in the SaaS you are using, within the test class initialization. For instance, if your regular database is `my_database`, then use `my_database_test_202508221000` using the date-time as part of the database name pattern, so you can tell when the test databases were created.
- Within the test initialization clean up the database (in case a previous test ran and did not clean up due to failure).
- With the test class tear-down, delete the test database, so you won't end up with multiple test database.
- Since sometime you may stop the tests before complete and thus skip the tear-down, you will need to either delete leftover test databases manually from time to time, or add a search for test databases from previous days to the test class initialization and remove them there.

In the test class and/or test initialization methods, add **mock data** your tests may depend on.

If your code searches the database for records from the past N days/months/etc. then enable inject "today"'s date into the business logic, so tests can override the date and thus enable testing on the data sample you have without too much effort in recreating the data relative to test run's date (unless doing so is easy).

The unit / component you test this way could be anything, including a method, a class, a microservice, etc.

---

**Navigation:**

- Previous: [Concurrency and databases summary](./concurrency-and-dbs-summary.md)
