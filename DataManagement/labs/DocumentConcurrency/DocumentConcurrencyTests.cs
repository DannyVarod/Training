using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json;

namespace DocumentConcurrency;

[TestClass]
public class DocumentConcurrencyTests
{
    private const string TestConnectionString = "mongodb://localhost:27017";
    private const string TestDatabaseName = "ConcurrencyTestDb";

    [TestMethod]
    public async Task NoConcurrencyControl_DemonstratesLostUpdates()
    {
        var initialAccount = new AccountForMongo
        {
            _id = Guid.NewGuid(),
            Name = "Test account 1",
            SavingsBalance = 10000.0M,
            CheckingBalance = 0.0M,
        };

        // Use an in-memory collection simulation for testing
        var collection = GetTestCollection<AccountForMongo>("accounts_no_concurrency");

        // Insert account
        Console.WriteLine(JsonSerializer.Serialize(initialAccount));
        await collection.InsertOneAsync(initialAccount);

        int requests = 100;
        var tasks = Enumerable.Range(0, requests).Select(async i =>
        {
            // This demonstrates the problem: read-modify-write without atomicity
            var account = await collection.Find(a => a._id == initialAccount._id).FirstOrDefaultAsync();
            
            account.SavingsBalance -= 100.0M;
            account.CheckingBalance += 100.0M;
            
            // ReplaceOneAsync without atomic operations can cause lost updates
            await collection.ReplaceOneAsync<AccountForMongo>(a => a._id == initialAccount._id, account);
        });

        await Task.WhenAll(tasks);

        // Check results - without atomic operations, we expect lost updates
        var resultingAccount = await collection.Find(a => a._id == initialAccount._id).FirstOrDefaultAsync();

        Console.WriteLine($"Expected Savings Balance: {initialAccount.SavingsBalance - 100.0M * requests}");
        Console.WriteLine($"Actual Savings Balance: {resultingAccount.SavingsBalance}");
        Console.WriteLine($"Expected Checking Balance: {initialAccount.CheckingBalance + 100.0M * requests}");
        Console.WriteLine($"Actual Checking Balance: {resultingAccount.CheckingBalance}");

        // The total balance should remain constant, but without concurrency control it might not
        var totalBalance = resultingAccount.SavingsBalance + resultingAccount.CheckingBalance;
        var expectedTotal = initialAccount.SavingsBalance + initialAccount.CheckingBalance;
        
        Console.WriteLine($"Total balance should remain: {expectedTotal}, actual: {totalBalance}");
        Console.WriteLine("⚠️  Without atomic operations, some updates may be lost due to race conditions");
    }

    [TestMethod]
    public async Task AtomicOperations_PreventsLostUpdates()
    {
        var initialAccount = new AccountForMongo
        {
            _id = Guid.NewGuid(),
            Name = "Test account 1",
            SavingsBalance = 10000.0M,
            CheckingBalance = 0.0M,
        };

        var collection = GetTestCollection<AccountForMongo>("accounts_atomic");

        // Insert account
        Console.WriteLine(JsonSerializer.Serialize(initialAccount));
        await collection.InsertOneAsync(initialAccount);

        int requests = 100;
        var tasks = Enumerable.Range(0, requests).Select(async i =>
        {
            // Atomic update operations using MongoDB update operators
            var update1 = Builders<AccountForMongo>.Update.Inc(a => a.SavingsBalance, -100.0M);
            var update2 = Builders<AccountForMongo>.Update.Inc(a => a.CheckingBalance, 100.0M);
            var combinedUpdate = Builders<AccountForMongo>.Update.Combine(update1, update2);
            
            await collection.UpdateOneAsync<AccountForMongo>(a => a._id == initialAccount._id, combinedUpdate);
        });

        await Task.WhenAll(tasks);

        // Check results - with atomic operations, all updates should be applied correctly
        var resultingAccount = await collection.Find(a => a._id == initialAccount._id).FirstOrDefaultAsync();

        var expectedSavings = initialAccount.SavingsBalance - 100.0M * requests;
        var expectedChecking = initialAccount.CheckingBalance + 100.0M * requests;

        Assert.AreEqual(expectedSavings, resultingAccount.SavingsBalance,
            "Atomic operations should prevent lost updates in savings");
        Assert.AreEqual(expectedChecking, resultingAccount.CheckingBalance,
            "Atomic operations should prevent lost updates in checking");

        Console.WriteLine($"✅ All {requests} updates applied correctly with atomic operations");
        Console.WriteLine($"Savings: {resultingAccount.SavingsBalance}, Checking: {resultingAccount.CheckingBalance}");
    }

    [TestMethod]
    public async Task ComplexAtomicOperations_MultipleFields()
    {
        var initialAccount = new BankAccount
        {
            _id = Guid.NewGuid(),
            AccountNumber = "ACC001",
            Balance = 50000.0M,
            TransactionCount = 0,
            LastTransactionDate = DateTime.UtcNow.AddDays(-30),
            AccountType = "Savings"
        };

        var collection = GetTestCollection<BankAccount>("bank_accounts");
        await collection.InsertOneAsync(initialAccount);

        // Perform 100 concurrent transactions with complex atomic updates
        int transactions = 100;
        decimal transactionAmount = 150.0M;

        var tasks = Enumerable.Range(0, transactions).Select(async i =>
        {
            // Complex atomic update: modify balance, increment counter, update timestamp
            var update = Builders<BankAccount>.Update
                .Inc(a => a.Balance, i % 2 == 0 ? transactionAmount : -transactionAmount)
                .Inc(a => a.TransactionCount, 1)
                .Set(a => a.LastTransactionDate, DateTime.UtcNow);

            await collection.UpdateOneAsync(a => a._id == initialAccount._id, update);
        });

        await Task.WhenAll(tasks);

        var finalAccount = await collection.Find(a => a._id == initialAccount._id).FirstOrDefaultAsync();

        // Verify transaction count
        Assert.AreEqual(initialAccount.TransactionCount + transactions, finalAccount.TransactionCount,
            "Transaction count should be accurate with atomic increments");

        // Verify balance consistency (50 deposits of +150, 50 withdrawals of -150 = net 0)
        var expectedBalance = initialAccount.Balance;
        Assert.AreEqual(expectedBalance, finalAccount.Balance,
            "Balance should remain unchanged with equal deposits and withdrawals");

        Console.WriteLine($"✅ Complex atomic operations completed successfully");
        Console.WriteLine($"Final balance: {finalAccount.Balance}, Transaction count: {finalAccount.TransactionCount}");
        Console.WriteLine($"Last transaction: {finalAccount.LastTransactionDate}");
    }

    [TestMethod]
    public async Task ConditionalAtomicUpdates_WithFilters()
    {
        var accounts = new[]
        {
            new BankAccount 
            { 
                _id = Guid.NewGuid(), 
                AccountNumber = "ACC001", 
                Balance = 1000.0M, 
                TransactionCount = 0,
                AccountType = "Checking"
            },
            new BankAccount 
            { 
                _id = Guid.NewGuid(), 
                AccountNumber = "ACC002", 
                Balance = 500.0M, 
                TransactionCount = 0,
                AccountType = "Savings"
            }
        };

        var collection = GetTestCollection<BankAccount>("conditional_accounts");
        await collection.InsertManyAsync(accounts);

        // Attempt concurrent withdrawals with balance checks
        int attempts = 200;
        decimal withdrawalAmount = 100.0M;
        int successfulWithdrawals = 0;

        var tasks = Enumerable.Range(0, attempts).Select(async i =>
        {
            var targetAccount = accounts[i % 2]; // Alternate between accounts

            // Conditional atomic update: only withdraw if sufficient balance
            var filter = Builders<BankAccount>.Filter.And(
                Builders<BankAccount>.Filter.Eq(a => a._id, targetAccount._id),
                Builders<BankAccount>.Filter.Gte(a => a.Balance, withdrawalAmount)
            );

            var update = Builders<BankAccount>.Update
                .Inc(a => a.Balance, -withdrawalAmount)
                .Inc(a => a.TransactionCount, 1)
                .Set(a => a.LastTransactionDate, DateTime.UtcNow);

            var result = await collection.UpdateOneAsync(filter, update);
            
            if (result.ModifiedCount > 0)
            {
                Interlocked.Increment(ref successfulWithdrawals);
            }
        });

        await Task.WhenAll(tasks);

        // Verify results
        var accountIds = accounts.Select(acc => acc._id).ToList();
        var finalAccounts = await collection.Find(a => accountIds.Contains(a._id)).ToListAsync();

        // Ensure no account has negative balance
        foreach (var account in finalAccounts)
        {
            Assert.IsTrue(account.Balance >= 0, 
                $"Account {account.AccountNumber} should not have negative balance");
        }

        var totalFinalBalance = finalAccounts.Sum(a => a.Balance);
        var totalWithdrawn = successfulWithdrawals * withdrawalAmount;
        var expectedFinalBalance = 1500.0M - totalWithdrawn; // Initial total minus withdrawals

        Assert.AreEqual(expectedFinalBalance, totalFinalBalance,
            "Total balance should equal initial balance minus successful withdrawals");

        Console.WriteLine($"✅ Conditional atomic updates completed successfully");
        Console.WriteLine($"Successful withdrawals: {successfulWithdrawals} out of {attempts} attempts");
        Console.WriteLine($"Final total balance: {totalFinalBalance}");
    }

    [TestMethod]
    public async Task ArrayFieldUpdates_AtomicOperations()
    {
        var account = new AccountWithTransactions
        {
            _id = Guid.NewGuid(),
            AccountNumber = "TXN001",
            Balance = 10000.0M,
            Transactions = new List<Transaction>()
        };

        var collection = GetTestCollection<AccountWithTransactions>("account_transactions");
        await collection.InsertOneAsync(account);

        // Concurrent addition of transactions using atomic array operations
        int transactionCount = 100;
        var tasks = Enumerable.Range(0, transactionCount).Select(async i =>
        {
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = (i % 2 == 0 ? 1 : -1) * 50.0M,
                Description = $"Transaction {i}",
                Timestamp = DateTime.UtcNow
            };

            // Atomic operations: push to array and update balance
            var update = Builders<AccountWithTransactions>.Update
                .Push(a => a.Transactions, transaction)
                .Inc(a => a.Balance, transaction.Amount);

            await collection.UpdateOneAsync(a => a._id == account._id, update);
        });

        await Task.WhenAll(tasks);

        var finalAccount = await collection.Find(a => a._id == account._id).FirstOrDefaultAsync();

        // Verify transaction count
        Assert.AreEqual(transactionCount, finalAccount.Transactions.Count,
            "All transactions should be atomically added to array");

        // Verify balance consistency
        var calculatedBalance = account.Balance + finalAccount.Transactions.Sum(t => t.Amount);
        Assert.AreEqual(calculatedBalance, finalAccount.Balance,
            "Balance should match sum of all transactions");

        Console.WriteLine($"✅ Array field atomic operations completed successfully");
        Console.WriteLine($"Transaction count: {finalAccount.Transactions.Count}");
        Console.WriteLine($"Final balance: {finalAccount.Balance}");
    }

    [TestMethod]
    public async Task UpsertOperations_AtomicCreateOrUpdate()
    {
        var collection = GetTestCollection<UserSession>("user_sessions");
        
        var userId = "user_123";
        int concurrentSessions = 50;

        // Simulate concurrent login attempts that should create or update user session
        var tasks = Enumerable.Range(0, concurrentSessions).Select(async i =>
        {
            var filter = Builders<UserSession>.Filter.Eq(s => s.UserId, userId);
            
            var update = Builders<UserSession>.Update
                .Set(s => s.LastLoginTime, DateTime.UtcNow)
                .Inc(s => s.LoginCount, 1)
                .SetOnInsert(s => s._id, Guid.NewGuid())
                .SetOnInsert(s => s.UserId, userId)
                .SetOnInsert(s => s.FirstLoginTime, DateTime.UtcNow);

            // Upsert: atomic create-or-update operation
            await collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        });

        await Task.WhenAll(tasks);

        // Verify only one session document was created
        var sessions = await collection.Find(s => s.UserId == userId).ToListAsync();

        Assert.AreEqual(1, sessions.Count, "Only one session document should exist per user");

        var session = sessions.First();
        Assert.AreEqual(concurrentSessions, session.LoginCount,
            "Login count should reflect all concurrent operations");

        Console.WriteLine($"✅ Upsert operations completed successfully");
        Console.WriteLine($"Login count: {session.LoginCount}, Session ID: {session._id}");
    }

    private IMongoCollection<T> GetTestCollection<T>(string collectionName)
    {
        // For testing purposes, we'll use a mock setup or in-memory equivalent
        // In a real scenario, this would connect to a MongoDB instance
        try
        {
            // Configure MongoDB client with standard UUID representation
            var settings = MongoClientSettings.FromConnectionString(TestConnectionString);
            settings.GuidRepresentation = GuidRepresentation.Standard;
            
            var client = new MongoClient(settings);
            var database = client.GetDatabase($"{TestDatabaseName}_{DateTime.Now:yyyyMMddHHmmss}");
            return database.GetCollection<T>(collectionName);
        }
        catch
        {
            // If MongoDB is not available, throw a more descriptive error
            throw new InvalidOperationException(
                "MongoDB connection failed. Please ensure MongoDB is running or modify the connection string. " +
                "For testing purposes, you can use MongoDB Docker container: docker run -d -p 27017:27017 mongo:latest");
        }
    }
}

// Models for testing

public class AccountForMongo
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid _id { get; set; } // MongoDB requires _id field
    public required string Name { get; set; }
    public Decimal SavingsBalance { get; set; }
    public Decimal CheckingBalance { get; set; }
}

public class BankAccount
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid _id { get; set; }
    public required string AccountNumber { get; set; }
    public Decimal Balance { get; set; }
    public int TransactionCount { get; set; }
    public DateTime LastTransactionDate { get; set; }
    public required string AccountType { get; set; }
}

public class Transaction
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }
    public Decimal Amount { get; set; }
    public required string Description { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AccountWithTransactions
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid _id { get; set; }
    public required string AccountNumber { get; set; }
    public Decimal Balance { get; set; }
    public List<Transaction> Transactions { get; set; } = new();
}

public class UserSession
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid _id { get; set; }
    public required string UserId { get; set; }
    public DateTime FirstLoginTime { get; set; }
    public DateTime LastLoginTime { get; set; }
    public int LoginCount { get; set; }
}
