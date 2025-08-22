# Concurrency and key-value databases

Key-value databases like Redis provide atomic operations that can eliminate concurrency issues when used correctly. However, when developers treat them like simple storage and use read-modify-write patterns, they encounter the same lost update problems as other database types. This section demonstrates both problematic and correct approaches to concurrency in key-value databases.

## Bad Example: JSON Serialization with Read-Modify-Write

When developers store complex objects as JSON strings and use read-modify-write patterns, they lose the benefits of atomic operations that key-value databases provide.

### Product Model for JSON Storage

```csharp
// Product.cs - Model for JSON serialization
using System.Text.Json.Serialization;

public class ProductData
{
    public Guid Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
    public int Quantity { get; set; }
    
    public long ViewCount { get; set; }
    
    public List<string> Categories { get; set; } = new();
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}
```

### Unsafe JSON-Based Service

```csharp
// UnsafeJsonProductService.cs - Demonstrating the wrong way
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public class UnsafeJsonProductService
{
    private readonly IDatabase _database;
    private readonly ILogger<UnsafeJsonProductService> _logger;
    
    public UnsafeJsonProductService(IConnectionMultiplexer redis, ILogger<UnsafeJsonProductService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }
    
    // Thread 1: Create initial product
    public async Task<Guid> CreateProductAsync(string name, decimal price, int quantity)
    {
        var productId = Guid.NewGuid();
        var product = new ProductData
        {
            Id = productId,
            Name = name,
            Price = price,
            Quantity = quantity,
            ViewCount = 0,
            Categories = new List<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var json = JsonSerializer.Serialize(product);
        var key = $"my_app:product:{productId}";
        
        await _database.StringSetAsync(key, json);
        
        _logger.LogInformation("Product {ProductId} created", productId);
        return productId;
    }
    
    // Multiple threads: Unsafe JSON update - WILL CAUSE LOST UPDATES
    public async Task<bool> UnsafeIncrementViewCountAsync(Guid productId, int incrementBy, int threadId)
    {
        try
        {
            var key = $"my_app:product:{productId}";
            
            // Step 1: Read current JSON value
            var currentJson = await _database.StringGetAsync(key);
            if (!currentJson.HasValue)
            {
                _logger.LogWarning("Product {ProductId} not found by Thread {ThreadId}", productId, threadId);
                return false;
            }
            
            // Step 2: Deserialize JSON
            var product = JsonSerializer.Deserialize<ProductData>(currentJson!);
            if (product == null)
            {
                _logger.LogError("Failed to deserialize product {ProductId} by Thread {ThreadId}", productId, threadId);
                return false;
            }
            
            var originalViewCount = product.ViewCount;
            _logger.LogInformation("Thread {ThreadId} read current view count: {CurrentCount}, adding: {IncrementBy}",
                threadId, originalViewCount, incrementBy);
            
            // Step 3: Simulate processing time - increases race condition window
            await Task.Delay(50);
            
            // Step 4: Modify the object (RACE CONDITION: other threads might have modified it)
            product.ViewCount += incrementBy;
            product.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Thread {ThreadId} calculated new view count: {NewCount}",
                threadId, product.ViewCount);
            
            // Step 5: Serialize and write back (LOST UPDATE PROBLEM)
            var updatedJson = JsonSerializer.Serialize(product);
            await _database.StringSetAsync(key, updatedJson);
            
            _logger.LogInformation("Thread {ThreadId} completed JSON update for product {ProductId}",
                threadId, productId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thread {ThreadId} encountered error updating product {ProductId}",
                threadId, productId);
            return false;
        }
    }
    
    // Read current state
    public async Task<ProductData?> GetProductAsync(Guid productId)
    {
        var key = $"my_app:product:{productId}";
        var json = await _database.StringGetAsync(key);
        
        if (!json.HasValue)
            return null;
            
        return JsonSerializer.Deserialize<ProductData>(json!);
    }
}
```

## Good Example: Using Redis Hash Operations

The correct approach uses Redis's native data structures and atomic operations, particularly hash operations for storing object fields separately.

### Safe Hash-Based Service

```csharp
// SafeHashProductService.cs - Demonstrating the right way
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

public class SafeHashProductService
{
    private readonly IDatabase _database;
    private readonly ILogger<SafeHashProductService> _logger;
    
    public SafeHashProductService(IConnectionMultiplexer redis, ILogger<SafeHashProductService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }
    
    // Thread 1: Create initial product using hash
    public async Task<Guid> CreateProductAsync(string name, decimal price, int quantity)
    {
        var productId = Guid.NewGuid();
        var key = $"my_app:product:{productId}";
        
        // Use Redis hash to store individual fields
        var hash = new HashEntry[]
        {
            new("id", productId.ToString()),
            new("name", name),
            new("price", price.ToString()),
            new("quantity", quantity.ToString()),
            new("viewCount", "0"),
            new("categories", ""), // Empty for simplicity
            new("createdAt", DateTime.UtcNow.ToString("O")),
            new("updatedAt", DateTime.UtcNow.ToString("O"))
        };
        
        await _database.HashSetAsync(key, hash);
        
        _logger.LogInformation("Product {ProductId} created using hash", productId);
        return productId;
    }
    
    // Multiple threads: Safe atomic increment - NO lost updates
    public async Task<bool> SafeIncrementViewCountAsync(Guid productId, int incrementBy, int threadId)
    {
        try
        {
            var key = $"my_app:product:{productId}";
            
            // Check if product exists
            var exists = await _database.KeyExistsAsync(key);
            if (!exists)
            {
                _logger.LogWarning("Product {ProductId} not found by Thread {ThreadId}", productId, threadId);
                return false;
            }
            
            _logger.LogInformation("Thread {ThreadId} attempting atomic increment by {IncrementBy} for product {ProductId}",
                threadId, incrementBy, productId);
            
            // Atomic operations using Redis commands
            var batch = _database.CreateBatch();
            
            // HINCRBY is atomic - increments hash field by specified amount
            var incrementTask = batch.HashIncrementAsync(key, "viewCount", incrementBy);
            
            // HSET is atomic - sets hash field value
            var updateTimeTask = batch.HashSetAsync(key, "updatedAt", DateTime.UtcNow.ToString("O"));
            
            // Execute batch atomically
            batch.Execute();
            
            var newViewCount = await incrementTask;
            await updateTimeTask;
            
            _logger.LogInformation("Thread {ThreadId} successfully incremented view count by {IncrementBy} for product {ProductId}, new count: {NewCount}",
                threadId, incrementBy, productId, newViewCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thread {ThreadId} encountered error updating product {ProductId}",
                threadId, productId);
            return false;
        }
    }
    
    // Alternative: Single atomic operation
    public async Task<long> AtomicIncrementViewCountAsync(Guid productId, int incrementBy, int threadId)
    {
        var key = $"my_app:product:{productId}";
        
        _logger.LogInformation("Thread {ThreadId} performing single atomic increment by {IncrementBy} for product {ProductId}",
            threadId, incrementBy, productId);
        
        // Single atomic operation - Redis guarantees this is thread-safe
        var newCount = await _database.HashIncrementAsync(key, "viewCount", incrementBy);
        
        // Update timestamp separately (or use Lua script for full atomicity)
        await _database.HashSetAsync(key, "updatedAt", DateTime.UtcNow.ToString("O"));
        
        _logger.LogInformation("Thread {ThreadId} atomic increment completed, new count: {NewCount}",
            threadId, newCount);
        
        return newCount;
    }
    
    // Read current state from hash
    public async Task<ProductData?> GetProductAsync(Guid productId)
    {
        var key = $"my_app:product:{productId}";
        var hash = await _database.HashGetAllAsync(key);
        
        if (hash.Length == 0)
            return null;
        
        var hashDict = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        
        return new ProductData
        {
            Id = Guid.Parse(hashDict["id"]),
            Name = hashDict["name"],
            Price = decimal.Parse(hashDict["price"]),
            Quantity = int.Parse(hashDict["quantity"]),
            ViewCount = long.Parse(hashDict["viewCount"]),
            Categories = string.IsNullOrEmpty(hashDict["categories"]) 
                ? new List<string>() 
                : hashDict["categories"].Split(',').ToList(),
            CreatedAt = DateTime.Parse(hashDict["createdAt"]),
            UpdatedAt = DateTime.Parse(hashDict["updatedAt"])
        };
    }
    
    // Advanced: Using Lua script for multiple atomic operations
    public async Task<bool> LuaScriptIncrementAsync(Guid productId, int incrementBy, int threadId)
    {
        var key = $"my_app:product:{productId}";
        
        // Lua script ensures atomicity across multiple operations
        var script = @"
            if redis.call('EXISTS', KEYS[1]) == 0 then
                return 0
            end
            
            local newCount = redis.call('HINCRBY', KEYS[1], 'viewCount', ARGV[1])
            redis.call('HSET', KEYS[1], 'updatedAt', ARGV[2])
            
            return newCount
        ";
        
        var timestamp = DateTime.UtcNow.ToString("O");
        var result = await _database.ScriptEvaluateAsync(script, new RedisKey[] { key }, new RedisValue[] { incrementBy, timestamp });
        
        if (result.IsNull || (long)result == 0)
        {
            _logger.LogWarning("Product {ProductId} not found by Thread {ThreadId} using Lua script", productId, threadId);
            return false;
        }
        
        _logger.LogInformation("Thread {ThreadId} Lua script increment completed, new count: {NewCount}",
            threadId, (long)result);
        return true;
    }
}
```

## Demonstration Program

```csharp
// Program.cs - Comparing unsafe vs safe approaches
public class Program
{
    public static async Task Main(string[] args)
    {
        // Setup Redis connection and DI
        var services = new ServiceCollection()
            .AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect("localhost:6379"))
            .AddScoped<UnsafeJsonProductService>()
            .AddScoped<SafeHashProductService>()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
            .BuildServiceProvider();
        
        await DemonstrateUnsafeApproach(services);
        Console.WriteLine("\n" + new string('=', 60) + "\n");
        await DemonstrateSafeApproach(services);
    }
    
    private static async Task DemonstrateUnsafeApproach(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        var unsafeService = scope.ServiceProvider.GetRequiredService<UnsafeJsonProductService>();
        
        // Create initial product
        var productId = await unsafeService.CreateProductAsync("JSON Product", 99.99m, 100);
        
        Console.WriteLine("=== UNSAFE JSON APPROACH (LOST UPDATES) ===");
        
        // Create 100 concurrent threads using unsafe JSON operations
        var unsafeTasks = Enumerable.Range(1, 100)
            .Select(threadId => Task.Run(async () =>
            {
                using var scope = services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<UnsafeJsonProductService>();
                await service.UnsafeIncrementViewCountAsync(productId, 1, threadId);
            }))
            .ToArray();
        
        // Wait for all unsafe operations to complete
        await Task.WhenAll(unsafeTasks);
        
        // Check result
        var unsafeProduct = await unsafeService.GetProductAsync(productId);
        Console.WriteLine($"After UNSAFE JSON operations - View Count: {unsafeProduct?.ViewCount}");
        Console.WriteLine($"Expected: 100 (100 × 1), Actual: {unsafeProduct?.ViewCount}");
        Console.WriteLine($"Lost updates: {100 - (unsafeProduct?.ViewCount ?? 0)}");
        Console.WriteLine($"Result is deterministic: {(unsafeProduct?.ViewCount == 100 ? "✓ YES" : "✗ NO - Lost updates occurred!")}");
        
        if (unsafeProduct?.ViewCount < 100)
        {
            Console.WriteLine("🚨 JSON read-modify-write pattern caused lost updates!");
        }
    }
    
    private static async Task DemonstrateSafeApproach(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        var safeService = scope.ServiceProvider.GetRequiredService<SafeHashProductService>();
        
        // Create initial product
        var productId = await safeService.CreateProductAsync("Hash Product", 99.99m, 100);
        
        Console.WriteLine("=== SAFE HASH APPROACH (ATOMIC OPERATIONS) ===");
        
        // Create 100 concurrent threads using safe hash operations
        var safeTasks = Enumerable.Range(1, 100)
            .Select(threadId => Task.Run(async () =>
            {
                using var scope = services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<SafeHashProductService>();
                await service.AtomicIncrementViewCountAsync(productId, 1, threadId);
            }))
            .ToArray();
        
        // Wait for all safe operations to complete
        await Task.WhenAll(safeTasks);
        
        // Check result
        var safeProduct = await safeService.GetProductAsync(productId);
        Console.WriteLine($"After SAFE HASH operations - View Count: {safeProduct?.ViewCount}");
        Console.WriteLine($"Expected: 100 (100 × 1), Actual: {safeProduct?.ViewCount}");
        Console.WriteLine($"Result is deterministic: {(safeProduct?.ViewCount == 100 ? "✓ YES" : "✗ NO")}");
        
        if (safeProduct?.ViewCount == 100)
        {
            Console.WriteLine("✅ Atomic hash operations preserved all updates!");
        }
    }
}
```

## Key Redis Atomic Operations

### Hash Operations
- **HINCRBY**: Atomically increments hash field by specified amount
- **HINCRBYFLOAT**: Atomically increments hash field by floating point amount  
- **HSET**: Atomically sets hash field value
- **HMSET**: Atomically sets multiple hash fields
- **HMGET**: Atomically gets multiple hash field values
- **HGETALL**: Atomically gets all hash fields and values
- **HSETNX**: Sets hash field only if it doesn't exist
- **HEXISTS**: Checks if hash field exists
- **HDEL**: Atomically deletes hash fields
- **HKEYS**: Gets all field names in hash
- **HVALS**: Gets all values in hash
- **HSTRLEN**: Gets length of hash field value

### String Operations with TTL
- **INCR/INCRBY**: Atomically increments string value
- **DECR/DECRBY**: Atomically decrements string value
- **APPEND**: Atomically appends to string value
- **GETSET**: Atomically gets old value and sets new value
- **SET with EX**: Set with expiration time
- **SETEX**: Set with expiration in seconds
- **SETNX**: Set only if key doesn't exist
- **EXPIRE**: Set expiration on existing key
- **TTL**: Get time to live for key

### List Operations
- **LPUSH/RPUSH**: Atomically adds elements to list
- **LPOP/RPOP**: Atomically removes and returns elements
- **LLEN**: Atomically gets list length
- **LRANGE**: Gets range of elements from list
- **LTRIM**: Trims list to specified range
- **LINDEX**: Gets element at index
- **LSET**: Sets element at index

### Set Operations
- **SADD**: Atomically adds members to set
- **SREM**: Atomically removes members from set
- **SCARD**: Atomically gets set cardinality
- **SISMEMBER**: Checks if member exists in set
- **SMEMBERS**: Gets all members of set
- **SPOP**: Atomically removes and returns random member
- **SRANDMEMBER**: Gets random member without removing

### Sorted Set Operations (for Rankings/Leaderboards)
- **ZADD**: Adds member with score to sorted set
- **ZRANGE**: Gets range of members by rank
- **ZRANGEBYSCORE**: Gets members by score range
- **ZRANK**: Gets rank of member
- **ZINCRBY**: Atomically increments member score
- **ZREM**: Removes member from sorted set
- **ZCARD**: Gets number of members in sorted set

### Transaction Operations
- **WATCH**: Marks keys to be watched for changes
- **MULTI**: Starts transaction
- **EXEC**: Executes transaction
- **DISCARD**: Discards transaction
- **UNWATCH**: Unwatches all watched keys

## Comparison: JSON vs Hash Approach

| Aspect | JSON Serialization | Redis Hash Operations |
|--------|-------------------|----------------------|
| **Concurrency** | Read-modify-write race conditions | Atomic field operations |
| **Performance** | Full object serialization/deserialization | Only relevant fields modified |
| **Network Traffic** | Entire object transferred | Only modified fields |
| **Data Integrity** | Lost updates possible | Guaranteed atomic updates |
| **Complexity** | Simple but unsafe | Slightly more complex but safe |
| **Scalability** | Poor under high concurrency | Excellent under high concurrency |
| **Memory Usage** | Full object in memory | Field-level operations |

## Best Practices for Key-Value Database Concurrency

1. **Use Atomic Operations**: Leverage built-in atomic operations instead of read-modify-write patterns
2. **Prefer Hash Fields**: Store object properties as hash fields for granular atomic updates
3. **Use Lua Scripts**: For complex multi-operation atomicity requirements
4. **Avoid JSON for Mutable Data**: JSON serialization breaks atomicity
5. **Design Keys Properly**: Structure keys to support atomic operations on the right granularity
6. **Batch Related Operations**: Use transactions or pipelines for related atomic operations
7. **Handle Failures Gracefully**: Atomic operations can still fail due to network issues

## When to Use Each Approach

### Use JSON Serialization When:
- Data is read-only or write-once
- Single-threaded access
- Complete object replacement is needed
- Complex nested structures that don't need atomic field updates

### Use Hash Operations When:
- Multiple threads modify the same data
- Only specific fields need updates
- Atomic counters or metrics are required
- High concurrency is expected

## Advanced Redis Patterns and Examples

### Rate Limiting with Sliding Window

```csharp
public class RateLimitingService
{
    private readonly IDatabase _database;
    
    public async Task<bool> IsAllowedAsync(string userId, int maxRequests, TimeSpan window)
    {
        var key = $"my_app:rate_limit:{userId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;
        
        // Use Lua script for atomic sliding window rate limiting
        var script = @"
            local key = KEYS[1]
            local now = tonumber(ARGV[1])
            local window_start = tonumber(ARGV[2])
            local max_requests = tonumber(ARGV[3])
            
            -- Remove old entries outside the window
            redis.call('ZREMRANGEBYSCORE', key, 0, window_start)
            
            -- Count current requests in window
            local current_requests = redis.call('ZCARD', key)
            
            if current_requests >= max_requests then
                return 0  -- Rate limit exceeded
            end
            
            -- Add current request
            redis.call('ZADD', key, now, now)
            redis.call('EXPIRE', key, math.ceil(ARGV[4] / 1000))
            
            return 1  -- Request allowed
        ";
        
        var result = await _database.ScriptEvaluateAsync(script, 
            new RedisKey[] { key }, 
            new RedisValue[] { now, windowStart, maxRequests, window.TotalMilliseconds });
        
        return (long)result == 1;
    }
}
```

### Leaderboard Implementation

```csharp
public class LeaderboardService
{
    private readonly IDatabase _database;
    
    public async Task<long> UpdateScoreAsync(string userId, int scoreIncrement)
    {
        var key = "my_app:leaderboard:global";
        
        // ZINCRBY is atomic - perfect for leaderboards
        var newScore = await _database.SortedSetIncrementAsync(key, userId, scoreIncrement);
        
        // Set expiration for cleanup
        await _database.KeyExpireAsync(key, TimeSpan.FromDays(30));
        
        return (long)newScore;
    }
    
    public async Task<long?> GetUserRankAsync(string userId)
    {
        var key = "my_app:leaderboard:global";
        
        // ZREVRANK gives rank in descending order (highest scores first)
        var rank = await _database.SortedSetRankAsync(key, userId, Order.Descending);
        
        return rank?.HasValue == true ? rank.Value + 1 : null; // Convert to 1-based ranking
    }
    
    public async Task<(string UserId, double Score)[]> GetTopUsersAsync(int count = 10)
    {
        var key = "my_app:leaderboard:global";
        
        var results = await _database.SortedSetRangeByRankWithScoresAsync(
            key, 0, count - 1, Order.Descending);
        
        return results.Select(x => (x.Element.ToString(), x.Score)).ToArray();
    }
}
```

### Distributed Cache with Cache-Aside Pattern

```csharp
public class DistributedCacheService<T>
{
    private readonly IDatabase _database;
    private readonly ILogger<DistributedCacheService<T>> _logger;
    
    public async Task<T?> GetAsync<TKey>(TKey key, Func<TKey, Task<T?>> dataLoader, 
        TimeSpan? expiration = null) where TKey : notnull
    {
        var cacheKey = $"my_app:cache:{typeof(T).Name}:{key}";
        
        // Try to get from cache first
        var cachedValue = await _database.StringGetAsync(cacheKey);
        if (cachedValue.HasValue)
        {
            _logger.LogDebug("Cache hit for key {Key}", cacheKey);
            return JsonSerializer.Deserialize<T>(cachedValue!);
        }
        
        _logger.LogDebug("Cache miss for key {Key}", cacheKey);
        
        // Load from data source
        var data = await dataLoader(key);
        if (data != null)
        {
            // Cache the result
            var json = JsonSerializer.Serialize(data);
            await _database.StringSetAsync(cacheKey, json, expiration ?? TimeSpan.FromMinutes(30));
        }
        
        return data;
    }
    
    public async Task InvalidateAsync<TKey>(TKey key) where TKey : notnull
    {
        var cacheKey = $"my_app:cache:{typeof(T).Name}:{key}";
        await _database.KeyDeleteAsync(cacheKey);
    }
}
```

## Industry Endorsements: Uber's Redis Patterns

**Uber's Engineering Blog** documents their extensive use of Redis for:
- **Atomic operations for ride matching**: Using HINCRBY for real-time driver availability counts
- **Sorted sets for driver location ranking**: ZADD/ZRANGE for geographic proximity searches  
- **Hash operations for real-time driver state**: HSET/HGET for driver status and location data

## Advanced Atomic Operations Benefits

### Eliminate Infrastructure Complexity:
- **Distributed Locks**: No need for coordination across multiple processes
- **Complex Retry Logic**: Operations succeed or fail cleanly  
- **Race Condition Debugging**: Deterministic behavior under high concurrency

### Why JSON Serialization Breaks Atomicity:
- **Memory Optimization Loss**: Redis can't use specialized encodings for structured data
- **Partial Update Impossibility**: Must replace entire objects for any change
- **Query Limitations**: Can't leverage Redis's rich data structure commands

Key-value databases excel at providing atomic operations, but only when used correctly. The difference between safe and unsafe approaches can mean the difference between data integrity and lost updates in high-concurrency scenarios.

---

**Navigation:**

- Previous: [Concurrency and document databases](./concurrency-document-dbs.md)
- Next: [Concurrency and columnar databases](./concurrency-columnar-dbs.md)
