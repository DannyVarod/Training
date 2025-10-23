# Concurrency and document databases

Document databases like MongoDB provide atomic operations at the document level, offering a different approach to concurrency control compared to relational databases. MongoDB's atomic update operators eliminate many common concurrency issues without requiring explicit transactions.

## Concurrency Control Approaches

### No concurrency control (do NOT use this in real code!)

```csharp
public class AccountForMongo
{
    [BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)]
    public Guid _id { get; set; } // here the ID must be named "_id"
    public required string Name { get; set; }
    public Decimal SavingsBalance { get; set; }
    public Decimal CheckingBalance { get; set; }
}

var initialAccount = new AccountForMongo
{
    _id = Guid.NewGuid(), 
    Name = "Test account 1",
    SavingsBalance = 10000.0M,
    CheckingBalance = 0.0M,
};

var client = new MongoClient(this.mongoDbConntectionString);
var collection = client.GetDatabase("MyApp").GetCollection<AccountForMongo>("Accounts");

// Insert account
Console.WriteLine(JsonSerializer.Serialize(initialAccount));
await collection.InsertOneAsync(initialAccount);

int requests = 1000;
var tasks = Enumerable.Range(0, requests).Select(async i =>
{
    var account = await collection.Find(a => a._id == initialAccount._id).FirstOrDefaultAsync();
    account.SavingsBalance -= 100.0M;
    account.CheckingBalance += 100.0M;
    await collection.ReplaceOneAsync<AccountForMongo>(a => a._id == initialAccount._id, account);
});
await Task.WhenAll(tasks);
```

### Atomic Operations

```csharp
public class AccountForMongo
{
    [BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)]
    public Guid _id { get; set; } // here the ID must be named "_id"
    public required string Name { get; set; }
    public Decimal SavingsBalance { get; set; }
    public Decimal CheckingBalance { get; set; }
}

var initialAccount = new AccountForMongo
{
    _id = Guid.NewGuid(), 
    Name = "Test account 1",
    SavingsBalance = 10000.0M,
    CheckingBalance = 0.0M,
};

var client = new MongoClient(this.mongoDbConntectionString);
var collection = client.GetDatabase("MyApp").GetCollection<AccountForMongo>("Accounts");

// Insert account
Console.WriteLine(JsonSerializer.Serialize(initialAccount));
await collection.InsertOneAsync(initialAccount);

int requests = 1000;
var tasks = Enumerable.Range(0, requests).Select(async i =>
{
    // Atomic update operations
    var update1 = Builders<AccountForMongo>.Update.Inc<decimal>(a => a.SavingsBalance, -100.0M);
    var update2 = Builders<AccountForMongo>.Update.Inc<decimal>(a => a.CheckingBalance, 100.0M);
    var combinedUpdate = Builders<AccountForMongo>.Update.Combine(update1, update2);
    await collection.UpdateOneAsync<AccountForMongo>(a => a._id == initialAccount._id, combinedUpdate);
});
await Task.WhenAll(tasks);
```

### Testing the result

```csharp
var requests = 1000;
var resultingAccount = await collection.Find(a => a._id == initialAccount._id).FirstOrDefaultAsync();
Assert.AreEqual(initialAccount.SavingsBalance - 100.0M * requests, resultingAccount.SavingsBalance);
Assert.AreEqual(initialAccount.CheckingBalance + 100.0M * requests, resultingAccount.CheckingBalance);
// This will fail if you did not use a concurrency control mechanism
```

### Comparison of Approaches

Approach | How it works | Pros | cons
--- | --- | --- | ---
No concurrency control | It doesn't | Simple code | Updates are non-deterministic, non-idempotent, you never know what you are going to get
Atomic operations | Uses MongoDB atomic update operators to modify fields | No lost updates, good performance under concurrency, more complex code | Works on a single collection with a single set of updates, Limited to supported atomic operations

---

**Navigation:**

- Previous page: [Concurrency and relational databases](./concurrency-relational-dbs.md)
- Next page: [Concurrency and key-value databases](./concurrency-key-value-dbs.md)
