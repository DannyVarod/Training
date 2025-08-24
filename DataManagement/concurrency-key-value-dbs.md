# Concurrency and key-value databases

Key-value databases like Redis provide atomic operations that eliminate concurrency issues when used correctly. The key is using Redis's native atomic operations rather than treating it as simple storage.

## Basic Hash Operations Example (from InsertAndGet.cs)

```csharp
[TestMethod]
public void RedisTest()
{
    var id = Guid.NewGuid();
    var savingsBalance = 10000.0;
    var checkingBalance = 0.0;

    var redisClient = ConnectionMultiplexer.Connect(this.redisConnectionString);
    var redisDb = redisClient.GetDatabase();

    redisDb.HashSet(key: $"MyApp:Accounts:{id}", hashField: "SavingsBalance", value: savingsBalance);
    redisDb.HashSet(key: $"MyApp:Accounts:{id}", hashField: "CheckingBalance", value: checkingBalance);

    Console.WriteLine(id);

    var sb = redisDb.HashGet(key: $"MyApp:Accounts:{id}", hashField: "SavingsBalance");
    var cb = redisDb.HashGet(key: $"MyApp:Accounts:{id}", hashField: "CheckingBalance");
    Assert.AreEqual(savingsBalance, sb);
    Assert.AreEqual(checkingBalance, cb);
}
```

## Concurrent Operations Example (from DBWork.cs)

```csharp
[TestMethod]
public async Task RedisTest()
{
    Guid id = Guid.NewGuid();
    double savingsBalance = 1000000d;
    double checkingBalance = 0d;

    ConnectionMultiplexer redisClient = ConnectionMultiplexer.Connect(this.redisConnectionString);
    IDatabase redisDb = redisClient.GetDatabase();

    Console.WriteLine(id);

    await redisDb.HashSetAsync(key: $"MyApp:Accounts:{id}", hashField: "SavingsBalance", value: savingsBalance);
    await redisDb.HashSetAsync(key: $"MyApp:Accounts:{id}", hashField: "CheckingBalance", value: checkingBalance);

    Console.WriteLine(id);

    RedisValue sb = await redisDb.HashGetAsync(key: $"MyApp:Accounts:{id}", hashField: "SavingsBalance");
    RedisValue cb = await redisDb.HashGetAsync(key: $"MyApp:Accounts:{id}", hashField: "CheckingBalance");
    Assert.AreEqual(savingsBalance, sb);
    Assert.AreEqual(checkingBalance, cb);

    double moveVal = 10d;
    int requests = 1000;
    await Task.Run(() => Parallel.For(0, requests, async i =>
    {
        await redisDb.HashDecrementAsync($"MyApp:Accounts:{id}", "SavingsBalance", moveVal);
        await redisDb.HashIncrementAsync($"MyApp:Accounts:{id}", "CheckingBalance", moveVal);
    }));
    
    Assert.AreEqual(savingsBalance - moveVal * requests, await redisDb.HashGetAsync($"MyApp:Accounts:{id}", "SavingsBalance"));
    Assert.AreEqual(checkingBalance + moveVal * requests, await redisDb.HashGetAsync($"MyApp:Accounts:{id}", "CheckingBalance"));
}
```

## Key Benefits

- **Atomic `HINCRBY`/`HDECRBY` operations** - increment/decrement hash fields without race conditions  
- **No version fields needed** - Redis handles concurrency at the operation level
- **No retry logic required** - atomic operations always work correctly
- **Simple, clean code** - just use `HashIncrementAsync`/`HashDecrementAsync`
- **Perfect for counters** - ideal for scenarios like account balances, view counts, etc.

Redis atomic hash operations provide the simplest and most reliable approach to concurrency control, eliminating the complexity of traditional database locking mechanisms.

---

**Navigation:**

- Previous: [Concurrency and document databases](./concurrency-document-dbs.md)
- Next: [Concurrency and columnar databases](./concurrency-columnar-dbs.md)
