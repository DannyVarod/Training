# Concurrency and relational databases

Relational databases provide robust concurrency control mechanisms to handle multiple threads or processes accessing and modifying data simultaneously. This section covers optimistic concurrency control examples.

## Concurrency Control Approaches

### No concurrency control (do NOT use this in real code!)

```csharp
public class AccountForMssql
{
    public Guid AccountId { get; set; }
    public required string Name { get; set; }
    public Decimal SavingsBalance { get; set; }
    public Decimal CheckingBalance { get; set; }
}

public class SqlDbContext : DbContext
{
    private readonly string _connectionString;

    public SqlDbContext(string connectionString)
    {
        _connectionString = connectionString;
        this.ChangeTracker.AutoDetectChangesEnabled = true;
        this.Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connectionString);
    }

    public DbSet<AccountForMssql> Accounts { get; protected set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountForMssql>(
            b => {
                b.ToTable("Accounts");
                b.HasKey("AccountId");
                b.Property(e => e.Name).IsRequired(true).IsUnicode(true).HasMaxLength(100);
                b.Property(e => e.SavingsBalance).HasPrecision(precision: 19, scale: 4).IsRequired(true);
                b.Property(e => e.CheckingBalance).HasPrecision(precision: 19, scale: 4).IsRequired(true);
                b.HasIndex("Name");
            });
    }
}

// Create account
var initialAccount = new AccountForMssql
{
    AccountId = Guid.NewGuid(),
    Name = "Account",
    SavingsBalance = 100000.0m,
    CheckingBalance = 0.0m,
};

// Insert account
using (var context = new SqlDbContext(this.sqlConnectionString))
{
    await context.Accounts.AddAsync(initialAccount);
    await context.SaveChangesAsync();
}

// Run 1000 concurrent requests moving money between accounts
int requests = 1000;
await Task.Run(() => Parallel.For(0, requests, async i =>
{
    // Update with optimistic concurrency retry
    var attempts = 0;
    do
    {
        try
        {
            using (var context = new SqlDbContext(this.sqlConnectionString))
            {
                var account = await context.Accounts.Where(a => a.AccountId == account1.AccountId).SingleAsync();
                account.SavingsBalance -= 100.0M;
                account.CheckingBalance += 100.0M;
                await context.SaveChangesAsync();
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // If a concurrency conflict occurs, retry the entire operation (read + update + save)
            attempts++;
            continue;
        }
        break;
    } while (true); // In real code, limit the number of attempts to avoid infinite loops, throw if too many
});
```

### Optimistic Concurrency Control (for a single entity)

```csharp

// Model:
public class AccountForMssql
{
    public Guid AccountId { get; set; }
    public required string Name { get; set; }
    public Decimal SavingsBalance { get; set; }
    public Decimal CheckingBalance { get; set; }

    [Timestamp]
    [ConcurrencyCheck]
    public byte[] Version { get; set; } // Added for optimistic concurrency
}

// DbContext:
public class SqlDbContext : DbContext
{
    private readonly string _connectionString;

    public SqlDbContext(string connectionString)
    {
        _connectionString = connectionString;
        this.ChangeTracker.AutoDetectChangesEnabled = true;
        this.Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connectionString);
    }

    public DbSet<AccountForMssql> Accounts { get; protected set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountForMssql>(
            b => {
                b.ToTable("Accounts");
                b.HasKey("AccountId");
                b.Property(e => e.Name).IsRequired(true).IsUnicode(true).HasMaxLength(100);
                b.Property(e => e.SavingsBalance).HasPrecision(precision: 19, scale: 4).IsRequired(true);
                b.Property(e => e.CheckingBalance).HasPrecision(precision: 19, scale: 4).IsRequired(true);
                b.Property(e => e.Version).IsConcurrencyToken(true); // Added for optimistic concurrency
                b.HasIndex("Name");
            });
    }
}

// Create account
var initialAccount = new AccountForMssql
{
    AccountId = Guid.NewGuid(),
    Name = "Account",
    SavingsBalance = 100000.0m,
    CheckingBalance = 0.0m,
};

// Insert account
using (var context = new SqlDbContext(this.sqlConnectionString))
{
    await context.Accounts.AddAsync(initialAccount);
    await context.SaveChangesAsync();
}

// Run 1000 concurrent requests moving money between accounts
int requests = 1000;
await Task.Run(() => Parallel.For(0, requests, async i =>
{
    using (var context = new SqlDbContext(this.sqlConnectionString))
    {
        var account = await context.Accounts.Where(a => a.AccountId == account1.AccountId).SingleAsync();
        account.SavingsBalance -= 100.0M;
        account.CheckingBalance += 100.0M;
        await context.SaveChangesAsync();
    }
});
```

### Transactional Concurrency Control (= locks, slower however this works also for multiple related entities)

```csharp
// Create account
var initialAccount = new AccountForMssql
{
    AccountId = Guid.NewGuid(),
    Name = "Account",
    SavingsBalance = 100000.0m,
    CheckingBalance = 0.0m,
};

// Insert account
using (var context = new SqlDbContext(this.sqlConnectionString))
{
    await context.Accounts.AddAsync(initialAccount);
    await context.SaveChangesAsync();
}

// Run 1000 concurrent requests moving money between accounts
int requests = 1000;
await Task.Run(() => Parallel.For(0, requests, async i =>
{
    int attempts = 0;
    do
    {
        try
        {
            // Can be used in-addition to optimistic concurrency (for extra safety) or without it
            // try taking model + context from each of the previous examples and testing with this code
            using (SqlDbContext context = new SqlDbContext(this.sqlConnectionString))
            {
                AccountForMssql bankAccount = await context.Accounts
                    .Where(a => a.AccountId == account.AccountId)
                    .SingleAsync();
                bankAccount.SavingsBalance -= 100.0M;
                bankAccount.CheckingBalance += 100.0M;
                await context.SaveChangesAsync();
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            attempts++;
            continue;
        }
        break;
    }
    while (true);
}));
```

### Testing the result

```csharp
var requests = 1000;
AccountForMssql resultingAccount;
using (SqlDbContext context = new SqlDbContext(this.sqlConnectionString))
{
    resultingAccount = await context.Accounts
        .Where(a => a.AccountId == account.AccountId)
        .SingleAsync();
}
Assert.AreEqual(initialAccount.SavingsBalance - 100.0M * requests, resultingAccount.SavingsBalance);
Assert.AreEqual(initialAccount.CheckingBalance + 100.0M * requests, resultingAccount.CheckingBalance);
// This will fail if you did not use a concurrency control mechanism
```

### Comparison of Approaches

Approach | How it works | Pros | cons
--- | --- | --- | ---
No concurrency control | It doesn't | Simple code | Updates are non-deterministic, non-idempotent, you never know what you are going to get
Optimistic concurrency control | Uses a version/timestamp field to detect conflicts, retry on conflict | No lost updates, good performance under concurrency | Only works for single entities, requires retry logic
Transactional concurrency control | Uses database transactions and locks to ensure data integrity | No lost updates, works for multiple related entities | Slower performance due to locking, incorrect usage can lead to deadlocks, more complex code

---

**Navigation:**

- Previous: [Concurrency and databases](./concurrency-and-dbs.md)
- Next: [Concurrency and document databases](./concurrency-document-dbs.md)
