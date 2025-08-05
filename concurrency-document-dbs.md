# Concurrency and document databases

Document databases like MongoDB provide atomic operations at the document level, offering a different approach to concurrency control compared to relational databases. Instead of relying on optimistic concurrency or explicit transactions for simple operations, MongoDB's atomic update operators eliminate many common concurrency issues.

## Document-Level Atomicity

MongoDB guarantees that operations on a single document are atomic. This means that multiple fields within a document can be updated atomically without the need for explicit transactions, providing natural concurrency control for document-based operations.

### Key Atomic Update Operators

MongoDB provides several atomic update operators that prevent race conditions:

- **`$set`**: Sets field values
- **`$inc`**: Increments numeric field values atomically
- **`$push`**: Adds elements to arrays
- **`$pop`**: Removes elements from arrays
- **`$addToSet`**: Adds unique elements to arrays
- **`$unset`**: Removes fields from documents

## MongoDB Document Schema Example

```csharp
// Product.cs - Document model for MongoDB
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Product
{
    [BsonId]
    public Guid Id { get; set; }
    
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
    
    [BsonElement("price")]
    public decimal Price { get; set; }
    
    [BsonElement("quantity")]
    public int Quantity { get; set; }
    
    [BsonElement("viewCount")]
    public long ViewCount { get; set; }
    
    [BsonElement("categories")]
    public List<string> Categories { get; set; } = new();
    
    [BsonElement("reviews")]
    public List<Review> Reviews { get; set; } = new();
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class Review
{
    [BsonElement("reviewId")]
    public Guid ReviewId { get; set; }
    
    [BsonElement("userId")]
    public Guid UserId { get; set; }
    
    [BsonElement("rating")]
    public int Rating { get; set; }
    
    [BsonElement("comment")]
    public string Comment { get; set; } = string.Empty;
    
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }
}
```

## Atomic Operations in Practice

### MongoDB Service with Atomic Operations

```csharp
// ProductService.cs - Service demonstrating atomic operations
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Logging;

public class ProductService
{
    private readonly IMongoCollection<Product> _products;
    private readonly ILogger<ProductService> _logger;
    
    public ProductService(IMongoDatabase database, ILogger<ProductService> logger)
    {
        _products = database.GetCollection<Product>("products");
        _logger = logger;
    }
    
    // Thread 1: Create initial product using upsert
    public async Task<Guid> CreateOrUpdateProductAsync(Guid productId, string name, decimal price, int quantity)
    {
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        
        var update = Builders<Product>.Update
            .Set(p => p.Name, name)
            .Set(p => p.Price, price)
            .Set(p => p.Quantity, quantity)
            .Set(p => p.UpdatedAt, DateTime.UtcNow)
            .SetOnInsert(p => p.Id, productId)
            .SetOnInsert(p => p.CreatedAt, DateTime.UtcNow)
            .SetOnInsert(p => p.ViewCount, 0)
            .SetOnInsert(p => p.Categories, new List<string>())
            .SetOnInsert(p => p.Reviews, new List<Review>());
        
        var options = new UpdateOptions { IsUpsert = true };
        
        var result = await _products.UpdateOneAsync(filter, update, options);
        
        _logger.LogInformation("Product {ProductId} created/updated. Matched: {MatchedCount}, Modified: {ModifiedCount}, Upserted: {UpsertedId}",
            productId, result.MatchedCount, result.ModifiedCount, result.UpsertedId);
        
        return productId;
    }
    
    // Threads 2 & 3: Atomic increment operations - NO concurrency issues
    public async Task<bool> IncrementViewCountAsync(Guid productId, int incrementBy, int threadId)
    {
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        
        // Atomic increment operation - this is thread-safe and deterministic
        var update = Builders<Product>.Update
            .Inc(p => p.ViewCount, incrementBy)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        
        _logger.LogInformation("Thread {ThreadId} attempting to increment view count by {IncrementBy} for product {ProductId}",
            threadId, incrementBy, productId);
        
        var result = await _products.UpdateOneAsync(filter, update);
        
        if (result.MatchedCount > 0)
        {
            _logger.LogInformation("Thread {ThreadId} successfully incremented view count by {IncrementBy} for product {ProductId}",
                threadId, incrementBy, productId);
            return true;
        }
        
        _logger.LogWarning("Thread {ThreadId} could not find product {ProductId} to increment view count",
            threadId, productId);
        return false;
    }
    
    // Atomic array operations
    public async Task<bool> AddCategoryAsync(Guid productId, string category)
    {
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        
        // $addToSet ensures no duplicates in the array
        var update = Builders<Product>.Update
            .AddToSet(p => p.Categories, category)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        
        var result = await _products.UpdateOneAsync(filter, update);
        return result.MatchedCount > 0;
    }
    
    public async Task<bool> AddReviewAsync(Guid productId, Review review)
    {
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        
        // $push adds the review to the array
        var update = Builders<Product>.Update
            .Push(p => p.Reviews, review)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        
        var result = await _products.UpdateOneAsync(filter, update);
        return result.MatchedCount > 0;
    }
    
    public async Task<bool> RemoveOldestReviewAsync(Guid productId)
    {
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        
        // $pop with -1 removes the first element, 1 removes the last element
        var update = Builders<Product>.Update
            .PopFirst(p => p.Reviews)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        
        var result = await _products.UpdateOneAsync(filter, update);
        return result.MatchedCount > 0;
    }
    
    // Complex atomic update combining multiple operations
    public async Task<bool> ProcessPurchaseAsync(Guid productId, int quantityPurchased, Review review)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(p => p.Id, productId),
            Builders<Product>.Filter.Gte(p => p.Quantity, quantityPurchased) // Ensure sufficient quantity
        );
        
        var update = Builders<Product>.Update
            .Inc(p => p.Quantity, -quantityPurchased) // Decrease quantity
            .Inc(p => p.ViewCount, 1) // Increment view count
            .Push(p => p.Reviews, review) // Add review
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        
        var result = await _products.UpdateOneAsync(filter, update);
        
        if (result.MatchedCount == 0)
        {
            _logger.LogWarning("Purchase failed for product {ProductId} - insufficient quantity or product not found", productId);
            return false;
        }
        
        _logger.LogInformation("Purchase processed for product {ProductId}, quantity decreased by {Quantity}", 
            productId, quantityPurchased);
        return true;
    }
    
    // Read current state
    public async Task<Product?> GetProductAsync(Guid productId)
    {
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        return await _products.Find(filter).FirstOrDefaultAsync();
    }
    
    // Dangerous non-atomic operation example (for comparison)
    public async Task<bool> UnsafeIncrementViewCountAsync(Guid productId, int incrementBy, int threadId)
    {
        // THIS IS NOT THREAD-SAFE - demonstrates the problem with non-atomic operations
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        var product = await _products.Find(filter).FirstOrDefaultAsync();
        
        if (product == null)
        {
            _logger.LogWarning("Thread {ThreadId} could not find product {ProductId}", threadId, productId);
            return false;
        }
        
        _logger.LogInformation("Thread {ThreadId} read current view count: {CurrentCount}, will update to: {NewCount}",
            threadId, product.ViewCount, product.ViewCount + incrementBy);
        
        // Simulate processing time - increases chance of race condition
        await Task.Delay(50);
        
        // Race condition: another thread might have updated the document between read and write
        var update = Builders<Product>.Update
            .Set(p => p.ViewCount, product.ViewCount + incrementBy)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        
        var result = await _products.UpdateOneAsync(filter, update);
        
        _logger.LogInformation("Thread {ThreadId} completed unsafe update for product {ProductId}",
            threadId, productId);
        
        return result.MatchedCount > 0;
    }
}

// Program.cs - Demonstrating atomic vs non-atomic operations
public class Program
{
    public static async Task Main(string[] args)
    {
        // Setup MongoDB connection and DI
        var services = new ServiceCollection()
            .AddSingleton<IMongoClient>(sp => new MongoClient("mongodb://localhost:27017"))
            .AddSingleton<IMongoDatabase>(sp => sp.GetService<IMongoClient>()!.GetDatabase("concurrency_demo"))
            .AddScoped<ProductService>()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
            .BuildServiceProvider();
        
        using var scope = services.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<ProductService>();
        
        var productId = Guid.NewGuid();
        
        // Thread 1: Create initial product
        await productService.CreateOrUpdateProductAsync(productId, "Demo Product", 99.99m, 100);
        
        Console.WriteLine("=== ATOMIC INCREMENT OPERATIONS (THREAD-SAFE) ===");
        
        // Create 100 concurrent threads using ATOMIC operations
        var atomicTasks = Enumerable.Range(1, 100)
            .Select(threadId => Task.Run(async () =>
            {
                using var scope = services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ProductService>();
                await service.IncrementViewCountAsync(productId, 1, threadId);
            }))
            .ToArray();
        
        // Wait for all 100 atomic operations to complete
        await Task.WhenAll(atomicTasks);
        
        // Check result after atomic operations
        var productAfterAtomic = await productService.GetProductAsync(productId);
        Console.WriteLine($"After ATOMIC operations - View Count: {productAfterAtomic?.ViewCount}");
        Console.WriteLine($"Expected: 100 (100 × 1), Actual: {productAfterAtomic?.ViewCount}");
        Console.WriteLine($"Result is deterministic: {(productAfterAtomic?.ViewCount == 100 ? "✓ YES" : "✗ NO")}");
        
        // Reset for unsafe demonstration
        await productService.CreateOrUpdateProductAsync(productId, "Demo Product", 99.99m, 100);
        
        Console.WriteLine("\n=== NON-ATOMIC OPERATIONS (RACE CONDITIONS) ===");
        
        // Create 100 concurrent threads using NON-ATOMIC operations (for comparison)
        var unsafeTasks = Enumerable.Range(1, 100)
            .Select(threadId => Task.Run(async () =>
            {
                using var scope = services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ProductService>();
                await service.UnsafeIncrementViewCountAsync(productId, 1, threadId);
            }))
            .ToArray();
        
        // Wait for all 100 unsafe operations to complete
        await Task.WhenAll(unsafeTasks);
        
        // Check result after non-atomic operations
        var productAfterUnsafe = await productService.GetProductAsync(productId);
        Console.WriteLine($"After NON-ATOMIC operations - View Count: {productAfterUnsafe?.ViewCount}");
        Console.WriteLine($"Expected: 100 (100 × 1), Actual: {productAfterUnsafe?.ViewCount}");
        Console.WriteLine($"Result is deterministic: {(productAfterUnsafe?.ViewCount == 100 ? "✓ YES" : "✗ NO - Race condition occurred!")}");
    }
}
```

## Advanced Atomic Operations

### Conditional Updates with Atomic Operations

```csharp
// Advanced atomic operations with conditions
public class AdvancedProductService
{
    private readonly IMongoCollection<Product> _products;
    private readonly ILogger<AdvancedProductService> _logger;
    
    public AdvancedProductService(IMongoDatabase database, ILogger<AdvancedProductService> logger)
    {
        _products = database.GetCollection<Product>("products");
        _logger = logger;
    }
    
    // Atomic operation with complex conditions
    public async Task<bool> AtomicPriceUpdateAsync(Guid productId, decimal maxCurrentPrice, decimal newPrice)
    {
        // Only update if current price is less than or equal to maxCurrentPrice
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(p => p.Id, productId),
            Builders<Product>.Filter.Lte(p => p.Price, maxCurrentPrice)
        );
        
        var update = Builders<Product>.Update
            .Set(p => p.Price, newPrice)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        
        var result = await _products.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
    
    // Atomic increment with maximum limit
    public async Task<bool> IncrementWithLimitAsync(Guid productId, string field, int increment, int maxValue)
    {
        // Use aggregation pipeline for complex conditional logic
        var pipeline = new[]
        {
            new BsonDocument("$set", new BsonDocument
            {
                [field] = new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$lte", new BsonArray { new BsonDocument("$add", new BsonArray { $"${field}", increment }), maxValue }),
                    new BsonDocument("$add", new BsonArray { $"${field}", increment }),
                    $"${field}"
                }),
                ["updatedAt"] = DateTime.UtcNow
            })
        };
        
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        var result = await _products.UpdateOneAsync(filter, pipeline);
        
        return result.ModifiedCount > 0;
    }
    
    // Batch atomic operations
    public async Task<bool> BulkAtomicOperationsAsync(Dictionary<Guid, int> productQuantityUpdates)
    {
        var writes = new List<WriteModel<Product>>();
        
        foreach (var (productId, quantityChange) in productQuantityUpdates)
        {
            var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
            var update = Builders<Product>.Update
                .Inc(p => p.Quantity, quantityChange)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);
            
            writes.Add(new UpdateOneModel<Product>(filter, update));
        }
        
        var result = await _products.BulkWriteAsync(writes);
        return result.ModifiedCount == productQuantityUpdates.Count;
    }
}
```

## Key Benefits of Document Database Concurrency

### 1. Natural Atomicity
Document-level atomicity eliminates many concurrency issues that require complex solutions in relational databases:

```csharp
// Single atomic operation replaces multiple relational database operations
var update = Builders<Product>.Update
    .Inc(p => p.Quantity, -5)           // Decrease inventory
    .Inc(p => p.ViewCount, 1)           // Increment views
    .Push(p => p.Reviews, newReview)    // Add review
    .AddToSet(p => p.Categories, "sale") // Add category
    .Set(p => p.UpdatedAt, DateTime.UtcNow);

await _products.UpdateOneAsync(filter, update);
// All operations succeed or fail together - no partial updates
```

### 2. No Lost Updates with Atomic Operators
Using `$inc` prevents the classic "lost update" problem:

```csharp
// These concurrent operations will NEVER cause lost updates
await Task.WhenAll(
    _products.UpdateOneAsync(filter, Builders<Product>.Update.Inc(p => p.ViewCount, 1)),
    _products.UpdateOneAsync(filter, Builders<Product>.Update.Inc(p => p.ViewCount, 1)),
    _products.UpdateOneAsync(filter, Builders<Product>.Update.Inc(p => p.ViewCount, 1))
);
// Result is always deterministic: original_value + 3
```

### 3. Simplified Error Handling
No need for retry logic on concurrency conflicts when using atomic operations:

```csharp
// Simple, reliable operation - no DbUpdateConcurrencyException to handle
public async Task<bool> IncrementViewCount(Guid productId)
{
    var result = await _products.UpdateOneAsync(
        Builders<Product>.Filter.Eq(p => p.Id, productId),
        Builders<Product>.Update.Inc(p => p.ViewCount, 1)
    );
    
    return result.MatchedCount > 0; // Simple success check
}
```

## Comparison with Relational Database Concurrency

| Aspect | MongoDB Atomic Operations | Relational Database Approaches |
|--------|---------------------------|--------------------------------|
| **Scope** | Document-level atomicity | Row-level (optimistic) or transaction-level |
| **Complexity** | Simple single operations | Requires version fields or transactions |
| **Race Conditions** | Eliminated for atomic ops | Requires careful handling and retry logic |
| **Performance** | High throughput, low latency | May require locks or conflict resolution |
| **Error Handling** | Minimal - operations succeed or fail cleanly | Complex retry logic for concurrency conflicts |
| **Scalability** | Excellent for document-centric operations | Challenges with distributed scenarios |

## Best Practices for Document Database Concurrency

1. **Use Atomic Operations**: Prefer `$inc`, `$push`, `$set` over read-modify-write patterns
2. **Design for Document Atomicity**: Structure data to take advantage of document-level operations  
3. **Avoid Read-Modify-Write**: When possible, use atomic operators instead of reading then updating
4. **Use Upserts**: Leverage upsert operations for idempotent behavior
5. **Conditional Updates**: Use filtered updates for business logic constraints
6. **Batch Operations**: Use bulk writes for multiple related atomic operations

Document databases excel at scenarios where atomic operations at the document level can solve most concurrency requirements, providing simpler and more performant solutions compared to traditional transaction-based approaches.

---

**Navigation:**

- Previous: [Concurrency and relational databases](./concurrency-relational-dbs.md)
- Next: [Concurrency and key-value databases](./concurrency-key-value-dbs.md)
