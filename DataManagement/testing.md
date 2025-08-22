# Testing

When testing, it is important to be aware of how databases work behind the scenes and this can affect your tests.

The optimal way to test code depends on how the code works.

## Code Types

### Logic Only

Logic only - Some business logic, an algorithm, a unit of code that receives input and calculates the output without pulling data from an external system.

This code is optimal for **unit testing**. Mock internal logic-only units to isolate test to a single unit.

```csharp
// Example: Pure business logic - perfect for unit testing
public class PriceCalculator
{
    public decimal CalculateDiscount(decimal price, CustomerType customerType, int quantity)
    {
        var baseDiscount = customerType switch
        {
            CustomerType.Premium => 0.15m,
            CustomerType.Regular => 0.05m,
            _ => 0m
        };

        var volumeDiscount = quantity > 100 ? 0.10m : 0m;
        return price * (baseDiscount + volumeDiscount);
    }
}

// Unit test - no database needed, just mock internal dependencies
[Test]
public void CalculateDiscount_PremiumCustomerLargeOrder_ReturnsCorrectDiscount()
{
    // Arrange
    var calculator = new PriceCalculator();
    
    // Act
    var discount = calculator.CalculateDiscount(1000m, CustomerType.Premium, 150);
    
    // Assert
    Assert.AreEqual(250m, discount); // 15% + 10% = 25%
}
```

### Data Access

Data access - When you have code that reads from a database, you can NOT mock the way the database behaves, **especially if you are using a relational database**.

Inserting into a table can be different than inserting into a list, due to unique constraints and other constraints defined on the table.

```csharp
// This will throw a real database constraint violation
public async Task<User> CreateUserAsync(string email)
{
    var user = new User { Email = email };
    await _context.Users.AddAsync(user); // Real unique constraint on Email
    await _context.SaveChangesAsync();   // Database enforces the constraint
    return user;
}

// BAD: Mock won't enforce real database constraints
var mockRepo = new Mock<IUserRepository>();
mockRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
       .ThrowsAsync(new Exception("Email exists")); // This is fake behavior
```

Changing a row in one table can affect queries on another table, due to joins and foreign keys.

```csharp
// Deleting a user affects related orders due to foreign key relationships
await _context.Users.Where(u => u.UserId == 123).ExecuteDeleteAsync();

// This query will now return different results due to the cascade delete
var userOrders = await _context.Orders
    .Where(o => o.UserId == 123) // Will return empty due to FK constraint
    .ToListAsync();
```

This code is optimal for **component testing**.

## Steps for Testing with Data Access Code

### Step 1: Create a Short-Living Test Database

Create a short-living test database, either via a docker in the test/local machine, or in the SaaS you are using, within the test class initialization. For instance, if your regular database is `my_database`, then use `my_database_test_202508221000` using the date-time as part of the database name pattern, so you can tell when the test databases were created.

```csharp
// SQL Server - Create unique test database name with timestamp pattern
[OneTimeSetUp]
public async Task OneTimeSetUp()
{
    _testDatabaseName = $"my_database_test_{DateTime.Now:yyyyMMddHHmm}";
    
    var masterConnectionString = "Server=localhost;Database=master;Integrated Security=true;";
    using var connection = new SqlConnection(masterConnectionString);
    await connection.OpenAsync();
    await connection.ExecuteAsync($"CREATE DATABASE [{_testDatabaseName}]");
}
```

```csharp
// MongoDB - Create test database with timestamp using C# driver
[OneTimeSetUp]
public async Task OneTimeSetUp()
{
    _testDatabaseName = $"my_database_test_{DateTime.Now:yyyyMMddHHmm}";
    _client = new MongoClient("mongodb://localhost:27017");
    _database = _client.GetDatabase(_testDatabaseName);
}
```

### Step 2: Clean Up Database in Test Initialization

Within the test initialization clean up the database (in case a previous test ran and did not clean up due to failure).

```csharp
// SQL Server - Clean up the database before each test (in case previous test failed)
[SetUp]
public async Task SetUp()
{
    using var scope = _serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Remove all data from test tables
    context.Orders.RemoveRange(context.Orders);
    context.Users.RemoveRange(context.Users);
    await context.SaveChangesAsync();
}
```

```csharp
// MongoDB - Clean up collections before each test (in case previous test failed)
[SetUp]
public async Task SetUp()
{
    await _database.GetCollection<Order>("orders").DeleteManyAsync(FilterDefinition<Order>.Empty);
    await _database.GetCollection<User>("users").DeleteManyAsync(FilterDefinition<User>.Empty);
}
```

### Step 3: Delete Test Database in Tear-Down

With the test class tear-down, delete the test database, so you won't end up with multiple test databases.

```csharp
// SQL Server - Delete the test database to avoid accumulating test databases
[OneTimeTearDown]
public async Task OneTimeTearDown()
{
    _serviceProvider?.Dispose();
    
    var masterConnectionString = "Server=localhost;Database=master;Integrated Security=true;";
    using var connection = new SqlConnection(masterConnectionString);
    await connection.OpenAsync();
    
    await connection.ExecuteAsync($@"
        ALTER DATABASE [{_testDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        DROP DATABASE [{_testDatabaseName}];
    ");
}
```

```csharp
// MongoDB - Drop the entire test database to avoid accumulating test databases
[OneTimeTearDown]
public async Task OneTimeTearDown()
{
    await _client.DropDatabaseAsync(_testDatabaseName);
    _client?.Dispose();
}
```

### Step 4: Handle Leftover Test Databases

Since sometimes you may stop the tests before complete and thus skip the tear-down, you will need to either delete leftover test databases manually from time to time, or add a search for test databases from previous days to the test class initialization and remove them there.

```csharp
[OneTimeSetUp]
public async Task OneTimeSetUp()
{
    // Clean up old test databases from previous days (in case tests were interrupted)
    await CleanupOldTestDatabases();
    
    // Then create new test database
    _testDatabaseName = $"my_database_test_{DateTime.Now:yyyyMMddHHmm}";
    // ... rest of setup
}

private async Task CleanupOldTestDatabases()
{
    var connectionString = "Server=localhost;Database=master;Integrated Security=true;";
    using var connection = new SqlConnection(connectionString);
    
    // Find test databases older than 1 day
    var oldDatabases = await connection.QueryAsync<string>(@"
        SELECT name FROM sys.databases 
        WHERE name LIKE 'my_database_test_%' 
        AND create_date < DATEADD(day, -1, GETDATE())
    ");

    // Drop old test databases
    foreach (var dbName in oldDatabases)
    {
        try
        {
            await connection.ExecuteAsync($@"
                ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{dbName}];
            ");
        }
        catch (Exception ex)
        {
            // Log but don't fail - database might be in use
            Console.WriteLine($"Failed to drop old test database {dbName}: {ex.Message}");
        }
    }
}
```

### Step 5: Add Mock Data

In the test class and/or test initialization methods, add **mock data** your tests may depend on.

```csharp
// SQL Server - Add mock data that tests depend on
[SetUp]
public async Task SetUp()
{
    // Clean up first
    // ... cleanup code ...
    
    var testUsers = new[]
    {
        new User { Email = "john@test.com", FirstName = "John", LastName = "Doe" },
        new User { Email = "jane@test.com", FirstName = "Jane", LastName = "Smith" }
    };
    
    context.Users.AddRange(testUsers);
    await context.SaveChangesAsync();
}
```

```csharp
// MongoDB - Add mock data that tests depend on using C# driver
[SetUp]
public async Task SetUp()
{
    // Clean up first
    await _database.GetCollection<User>("users").DeleteManyAsync(FilterDefinition<User>.Empty);
    
    var testUsers = new[]
    {
        new User { Email = "john@test.com", FirstName = "John", LastName = "Doe" },
        new User { Email = "jane@test.com", FirstName = "Jane", LastName = "Smith" }
    };
    
    await _database.GetCollection<User>("users").InsertManyAsync(testUsers);
}
```

### Step 6: Inject "Today's" Date for Time-Dependent Tests

If your code searches the database for records from the past N days/months/etc. then enable injecting "today's" date into the business logic, so tests can override the date and thus enable testing on the data sample you have without too much effort in recreating the data relative to test run's date (unless doing so is easy).

```csharp
// Business logic that depends on current date
public class ReportService
{
    private readonly IUserRepository _userRepository;
    private readonly ITimeProvider _timeProvider; // Inject time dependency

    public async Task<List<User>> GetRecentUsersAsync(int days = 30)
    {
        // Use injected time provider instead of DateTime.Now
        var cutoffDate = _timeProvider.UtcNow.AddDays(-days);
        return await _userRepository.GetUsersCreatedSinceAsync(cutoffDate);
    }
}

// Time provider for dependency injection
public interface ITimeProvider
{
    DateTime UtcNow { get; }
}

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

```csharp
// Test with controlled time
[Test]
public async Task GetRecentUsers_WithFixedDate_ReturnsCorrectUsers()
{
    // Arrange - inject a fixed date for predictable testing
    var fixedTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    var mockTimeProvider = new Mock<ITimeProvider>();
    mockTimeProvider.Setup(t => t.UtcNow).Returns(fixedTime);
    
    var reportService = new ReportService(_userRepository, mockTimeProvider.Object);
    
    // Create test data with known dates relative to the fixed time
    var users = new[]
    {
        new User { Email = "recent@test.com", CreatedDate = fixedTime.AddDays(-5) }, // Recent
        new User { Email = "old@test.com", CreatedDate = fixedTime.AddDays(-45) }    // Old
    };
    
    context.Users.AddRange(users);
    await context.SaveChangesAsync();

    // Act
    var recentUsers = await reportService.GetRecentUsersAsync(30);

    // Assert - only the recent user should be returned
    Assert.AreEqual(1, recentUsers.Count);
    Assert.AreEqual("recent@test.com", recentUsers[0].Email);
}
```

## What You Can Test This Way

The unit/component you test this way could be anything, including a method, a class, a microservice, etc.

```csharp
// Testing a single method
[Test]
public async Task CreateUser_ValidEmail_CreatesUser()
{
    var user = await _userService.CreateUserAsync("test@example.com", "John", "Doe");
    Assert.IsNotNull(user);
    Assert.Greater(user.UserId, 0);
}

// Testing a class with multiple methods
[Test]
public async Task UserService_CompleteWorkflow_WorksCorrectly()
{
    var user = await _userService.CreateUserAsync("test@example.com", "John", "Doe");
    var foundUser = await _userService.GetUserByEmailAsync("test@example.com");
    await _userService.UpdateUserAsync(user.UserId, "Jane", "Smith");
    
    Assert.AreEqual(user.UserId, foundUser.UserId);
}

// Testing microservice endpoints
[Test]
public async Task UserController_CreateUser_ReturnsCreatedUser()
{
    var request = new CreateUserRequest { Email = "test@example.com", FirstName = "John" };
    var response = await _client.PostAsJsonAsync("/api/users", request);
    
    response.EnsureSuccessStatusCode();
    var user = await response.Content.ReadFromJsonAsync<User>();
    Assert.AreEqual("test@example.com", user.Email);
}
```

## Why This Approach Works

This approach ensures your tests verify real database behavior including:

- **Constraint violations**: Unique constraints, foreign key constraints, check constraints
- **Transaction behavior**: Rollbacks, isolation levels, deadlocks
- **Performance characteristics**: Index usage, query optimization
- **Data integrity**: Cascading deletes, triggers, computed columns

```csharp
// This test verifies real constraint behavior
[Test]
public async Task CreateUser_DuplicateEmail_ThrowsConstraintException()
{
    // First user creation succeeds
    await _userService.CreateUserAsync("duplicate@test.com", "John", "Doe");

    // Second user with same email should fail due to unique constraint
    var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
        _userService.CreateUserAsync("duplicate@test.com", "Jane", "Smith"));
    
    // Verify it's actually a constraint violation, not just any exception
    Assert.That(ex.InnerException?.Message, Contains.Substring("UNIQUE constraint"));
}
```

The key principle: **Test the real database behavior, not mocked behavior**. Your tests should catch the same issues that would occur in production.

---

**Navigation:**

- Previous: [Concurrency and databases summary](./concurrency-and-dbs-summary.md)
