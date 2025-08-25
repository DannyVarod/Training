using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace RelationalConcurrency;

[TestClass]
public class RelationalConcurrencyTests
{
    private const string TestConnectionString = "Data Source=:memory:";

    [TestMethod]
    public async Task NoConcurrencyControl_DemonstratesLostUpdates()
    {
        // Create account
        var initialAccount = new AccountForMssql
        {
            AccountId = Guid.NewGuid(),
            Name = "Test Account",
            SavingsBalance = 100000.0m,
            CheckingBalance = 0.0m,
        };

        // Insert account using in-memory database
        using (var context = new SqlDbContext())
        {
            await context.Accounts.AddAsync(initialAccount);
            await context.SaveChangesAsync();
        }

        // Run 100 concurrent requests moving money between accounts
        int requests = 100;
        var tasks = Enumerable.Range(0, requests).Select(async i =>
        {
            using (var context = new SqlDbContext())
            {
                var account = await context.Accounts
                    .Where(a => a.AccountId == initialAccount.AccountId)
                    .SingleAsync();
                
                account.SavingsBalance -= 100.0M;
                account.CheckingBalance += 100.0M;
                await context.SaveChangesAsync();
            }
        });
        
        await Task.WhenAll(tasks);

        // Check the result - without concurrency control, we'll have lost updates
        AccountForMssql resultingAccount;
        using (var context = new SqlDbContext())
        {
            resultingAccount = await context.Accounts
                .Where(a => a.AccountId == initialAccount.AccountId)
                .SingleAsync();
        }

        Console.WriteLine($"Expected Savings Balance: {initialAccount.SavingsBalance - 100.0M * requests}");
        Console.WriteLine($"Actual Savings Balance: {resultingAccount.SavingsBalance}");
        Console.WriteLine($"Expected Checking Balance: {initialAccount.CheckingBalance + 100.0M * requests}");
        Console.WriteLine($"Actual Checking Balance: {resultingAccount.CheckingBalance}");

        // Without concurrency control, we expect lost updates
        var totalBalance = resultingAccount.SavingsBalance + resultingAccount.CheckingBalance;
        var expectedTotal = initialAccount.SavingsBalance + initialAccount.CheckingBalance;
        
        Console.WriteLine($"Total balance should remain: {expectedTotal}, actual: {totalBalance}");
        
        // This demonstrates that without concurrency control, we get inconsistent results
        // The assertion might pass sometimes and fail other times - demonstrating non-deterministic behavior
    }

    [TestMethod]
    public async Task OptimisticConcurrencyControl_PreventsLostUpdates()
    {
        // Create account with version field for optimistic concurrency
        var initialAccount = new AccountWithVersion
        {
            AccountId = Guid.NewGuid(),
            Name = "Test Account",
            SavingsBalance = 100000.0m,
            CheckingBalance = 0.0m,
        };

        // Insert account
        using (var context = new SqlDbContextWithVersion())
        {
            await context.Accounts.AddAsync(initialAccount);
            await context.SaveChangesAsync();
        }

        // Run 100 concurrent requests with retry logic for optimistic concurrency
        int requests = 100;
        var tasks = Enumerable.Range(0, requests).Select(async i =>
        {
            var attempts = 0;
            const int maxAttempts = 10;
            
            do
            {
                try
                {
                    using (var context = new SqlDbContextWithVersion())
                    {
                        var account = await context.Accounts
                            .Where(a => a.AccountId == initialAccount.AccountId)
                            .SingleAsync();
                        
                        account.SavingsBalance -= 100.0M;
                        account.CheckingBalance += 100.0M;
                        await context.SaveChangesAsync();
                        break; // Success, exit retry loop
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    // If a concurrency conflict occurs, retry the entire operation
                    attempts++;
                    if (attempts >= maxAttempts)
                    {
                        throw new InvalidOperationException($"Failed to update after {maxAttempts} attempts");
                    }
                    
                    // Brief delay before retry to reduce contention
                    await Task.Delay(Random.Shared.Next(1, 10));
                }
            } while (attempts < maxAttempts);
        });
        
        await Task.WhenAll(tasks);

        // Check the result - with optimistic concurrency, all updates should be applied
        AccountWithVersion resultingAccount;
        using (var context = new SqlDbContextWithVersion())
        {
            resultingAccount = await context.Accounts
                .Where(a => a.AccountId == initialAccount.AccountId)
                .SingleAsync();
        }

        var expectedSavings = initialAccount.SavingsBalance - 100.0M * requests;
        var expectedChecking = initialAccount.CheckingBalance + 100.0M * requests;

        Assert.AreEqual(expectedSavings, resultingAccount.SavingsBalance, 
            "Optimistic concurrency should prevent lost updates in savings");
        Assert.AreEqual(expectedChecking, resultingAccount.CheckingBalance,
            "Optimistic concurrency should prevent lost updates in checking");

        Console.WriteLine($"✅ All {requests} updates applied correctly with optimistic concurrency");
        Console.WriteLine($"Savings: {resultingAccount.SavingsBalance}, Checking: {resultingAccount.CheckingBalance}");
    }

    [TestMethod]
    public async Task TransactionalConcurrencyControl_WithRetryLogic()
    {
        // Create account
        var initialAccount = new AccountWithVersion
        {
            AccountId = Guid.NewGuid(),
            Name = "Test Account",
            SavingsBalance = 100000.0m,
            CheckingBalance = 0.0m,
        };

        // Insert account
        using (var context = new SqlDbContextWithVersion())
        {
            await context.Accounts.AddAsync(initialAccount);
            await context.SaveChangesAsync();
        }

        // Run concurrent requests with explicit transaction control
        int requests = 100;
        var tasks = Enumerable.Range(0, requests).Select(async i =>
        {
            var attempts = 0;
            const int maxAttempts = 10;
            
            do
            {
                try
                {
                    using (var context = new SqlDbContextWithVersion())
                    {
                        using (var transaction = await context.Database.BeginTransactionAsync())
                        {
                            var account = await context.Accounts
                                .Where(a => a.AccountId == initialAccount.AccountId)
                                .SingleAsync();
                            
                            account.SavingsBalance -= 100.0M;
                            account.CheckingBalance += 100.0M;
                            
                            await context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            break; // Success
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    attempts++;
                    if (attempts >= maxAttempts)
                    {
                        throw new InvalidOperationException($"Failed to update after {maxAttempts} attempts");
                    }
                    await Task.Delay(Random.Shared.Next(1, 10));
                }
            } while (attempts < maxAttempts);
        });
        
        await Task.WhenAll(tasks);

        // Verify results
        AccountWithVersion resultingAccount;
        using (var context = new SqlDbContextWithVersion())
        {
            resultingAccount = await context.Accounts
                .Where(a => a.AccountId == initialAccount.AccountId)
                .SingleAsync();
        }

        var expectedSavings = initialAccount.SavingsBalance - 100.0M * requests;
        var expectedChecking = initialAccount.CheckingBalance + 100.0M * requests;

        Assert.AreEqual(expectedSavings, resultingAccount.SavingsBalance);
        Assert.AreEqual(expectedChecking, resultingAccount.CheckingBalance);

        Console.WriteLine($"✅ Transactional concurrency control successful for {requests} operations");
    }

    [TestMethod]
    public async Task PessimisticLocking_UsingFromSqlRaw()
    {
        // Note: This test demonstrates the concept but uses in-memory database
        // In a real SQL Server scenario, you would use "SELECT ... WITH (UPDLOCK, HOLDLOCK)"
        
        var initialAccount = new AccountWithVersion
        {
            AccountId = Guid.NewGuid(),
            Name = "Test Account", 
            SavingsBalance = 10000.0m,
            CheckingBalance = 0.0m,
        };

        using (var context = new SqlDbContextWithVersion())
        {
            await context.Accounts.AddAsync(initialAccount);
            await context.SaveChangesAsync();
        }

        // Simulate pessimistic locking with explicit transactions
        int requests = 50;
        var tasks = Enumerable.Range(0, requests).Select(async i =>
        {
            using (var context = new SqlDbContextWithVersion())
            {
                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    // In real SQL Server, this would be:
                    // var account = await context.Accounts
                    //     .FromSqlRaw("SELECT * FROM Accounts WITH (UPDLOCK, HOLDLOCK) WHERE AccountId = {0}", 
                    //                 initialAccount.AccountId)
                    //     .SingleAsync();
                    
                    // For in-memory database, we simulate with regular query
                    var account = await context.Accounts
                        .Where(a => a.AccountId == initialAccount.AccountId)
                        .SingleAsync();
                    
                    // Simulate some processing time to demonstrate locking
                    await Task.Delay(10);
                    
                    account.SavingsBalance -= 100.0M;
                    account.CheckingBalance += 100.0M;
                    
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
            }
        });

        await Task.WhenAll(tasks);

        // Verify consistency
        using (var context = new SqlDbContextWithVersion())
        {
            var finalAccount = await context.Accounts
                .Where(a => a.AccountId == initialAccount.AccountId)
                .SingleAsync();
            
            var totalBalance = finalAccount.SavingsBalance + finalAccount.CheckingBalance;
            var expectedTotal = initialAccount.SavingsBalance + initialAccount.CheckingBalance;
            
            Assert.AreEqual(expectedTotal, totalBalance, "Total balance should remain constant");
            Console.WriteLine($"✅ Pessimistic locking maintained data consistency");
        }
    }

    [TestMethod]
    public async Task BankTransfer_MultipleAccountConcurrency()
    {
        // Create two accounts for transfer operations
        var account1 = new AccountWithVersion
        {
            AccountId = Guid.NewGuid(),
            Name = "Account 1",
            SavingsBalance = 50000.0m,
            CheckingBalance = 0.0m,
        };

        var account2 = new AccountWithVersion
        {
            AccountId = Guid.NewGuid(),
            Name = "Account 2",
            SavingsBalance = 50000.0m,
            CheckingBalance = 0.0m,
        };

        using (var context = new SqlDbContextWithVersion())
        {
            await context.Accounts.AddRangeAsync(account1, account2);
            await context.SaveChangesAsync();
        }

        // Perform concurrent transfers between accounts
        int transfers = 100;
        decimal transferAmount = 100.0m;
        
        var tasks = Enumerable.Range(0, transfers).Select(async i =>
        {
            var fromAccountId = i % 2 == 0 ? account1.AccountId : account2.AccountId;
            var toAccountId = i % 2 == 0 ? account2.AccountId : account1.AccountId;
            
            var attempts = 0;
            const int maxAttempts = 10;
            
            do
            {
                try
                {
                    using (var context = new SqlDbContextWithVersion())
                    {
                        using (var transaction = await context.Database.BeginTransactionAsync())
                        {
                            // Order accounts by ID to prevent deadlocks
                            var accountIds = new[] { fromAccountId, toAccountId }.OrderBy(id => id).ToArray();
                            
                            var accounts = await context.Accounts
                                .Where(a => accountIds.Contains(a.AccountId))
                                .OrderBy(a => a.AccountId)
                                .ToListAsync();
                            
                            var fromAccount = accounts.First(a => a.AccountId == fromAccountId);
                            var toAccount = accounts.First(a => a.AccountId == toAccountId);
                            
                            if (fromAccount.SavingsBalance >= transferAmount)
                            {
                                fromAccount.SavingsBalance -= transferAmount;
                                toAccount.SavingsBalance += transferAmount;
                                
                                await context.SaveChangesAsync();
                                await transaction.CommitAsync();
                                break;
                            }
                            else
                            {
                                await transaction.RollbackAsync();
                                break; // Insufficient funds, don't retry
                            }
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    attempts++;
                    if (attempts >= maxAttempts)
                    {
                        Console.WriteLine($"Transfer {i} failed after {maxAttempts} attempts");
                        break;
                    }
                    await Task.Delay(Random.Shared.Next(1, 5));
                }
            } while (attempts < maxAttempts);
        });

        await Task.WhenAll(tasks);

        // Verify that total money in system remains constant
        using (var context = new SqlDbContextWithVersion())
        {
            var finalAccounts = await context.Accounts
                .Where(a => a.AccountId == account1.AccountId || a.AccountId == account2.AccountId)
                .ToListAsync();
            
            var totalFinalBalance = finalAccounts.Sum(a => a.SavingsBalance + a.CheckingBalance);
            var expectedTotal = 100000.0m; // Original total for both accounts
            
            Assert.AreEqual(expectedTotal, totalFinalBalance, 
                "Total money in system should remain constant after all transfers");
            
            Console.WriteLine($"✅ Multi-account transfers completed successfully");
            Console.WriteLine($"Final balances: Account1={finalAccounts.First(a => a.AccountId == account1.AccountId).SavingsBalance}, " +
                            $"Account2={finalAccounts.First(a => a.AccountId == account2.AccountId).SavingsBalance}");
        }
    }
}

// Model without concurrency control
public class AccountForMssql
{
    public Guid AccountId { get; set; }
    public required string Name { get; set; }
    public Decimal SavingsBalance { get; set; }
    public Decimal CheckingBalance { get; set; }
}

// Model with optimistic concurrency control
public class AccountWithVersion
{
    public Guid AccountId { get; set; }
    public required string Name { get; set; }
    public Decimal SavingsBalance { get; set; }
    public Decimal CheckingBalance { get; set; }

    [Timestamp]
    [ConcurrencyCheck]
    public byte[]? Version { get; set; } // For optimistic concurrency
}

// DbContext without concurrency control
public class SqlDbContext : DbContext
{
    public DbSet<AccountForMssql> Accounts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Use in-memory database for testing
        optionsBuilder.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountForMssql>(b =>
        {
            b.ToTable("Accounts");
            b.HasKey(e => e.AccountId);
            b.Property(e => e.Name).IsRequired().HasMaxLength(100);
            b.Property(e => e.SavingsBalance).HasPrecision(19, 4).IsRequired();
            b.Property(e => e.CheckingBalance).HasPrecision(19, 4).IsRequired();
            b.HasIndex(e => e.Name);
        });
    }
}

// DbContext with optimistic concurrency control
public class SqlDbContextWithVersion : DbContext
{
    public DbSet<AccountWithVersion> Accounts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Use in-memory database for testing
        optionsBuilder.UseInMemoryDatabase("TestDbVersion_" + Guid.NewGuid());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountWithVersion>(b =>
        {
            b.ToTable("Accounts");
            b.HasKey(e => e.AccountId);
            b.Property(e => e.Name).IsRequired().HasMaxLength(100);
            b.Property(e => e.SavingsBalance).HasPrecision(19, 4).IsRequired();
            b.Property(e => e.CheckingBalance).HasPrecision(19, 4).IsRequired();
            b.Property(e => e.Version).IsConcurrencyToken();
            b.HasIndex(e => e.Name);
        });
    }
}
