# Concurrency and relational databases

Relational databases provide robust concurrency control mechanisms to handle multiple threads or processes accessing and modifying data simultaneously. This section covers the problems that occur without concurrency control and two primary approaches to solve them: optimistic concurrency control and transaction-based concurrency control.

## No Concurrency Control

Without proper concurrency control, multiple threads modifying the same data can lead to lost updates and non-deterministic results. This demonstrates the classic "lost update" problem that concurrency control mechanisms are designed to prevent.

### Basic Table Definition

```sql
-- Simple SQL Server table without concurrency control
CREATE TABLE [dbo].[Products] (
    [ProductId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(200) NOT NULL,
    [Price] DECIMAL(18,2) NOT NULL,
    [Quantity] INT NOT NULL,
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
);

-- Index for better performance on lookups
CREATE INDEX IX_Products_Name ON [dbo].[Products] ([Name]);
```

### Entity Framework Model (No Concurrency Control)

```csharp
// SimpleProduct.cs - Entity model without concurrency control
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SimpleProduct
{
    public Guid ProductId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    
    public int Quantity { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}

// SimpleDbContext.cs - DbContext without concurrency control
using Microsoft.EntityFrameworkCore;

public class SimpleDbContext : DbContext
{
    public SimpleDbContext(DbContextOptions<SimpleDbContext> options) : base(options) { }
    
    public DbSet<SimpleProduct> Products { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SimpleProduct>(entity =>
        {
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
    
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<SimpleProduct>()
            .Where(e => e.State == EntityState.Modified);
            
        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

### Unsafe Service Demonstrating Lost Updates

```csharp
// UnsafeProductService.cs - Service WITHOUT concurrency control
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class UnsafeProductService
{
    private readonly SimpleDbContext _context;
    private readonly ILogger<UnsafeProductService> _logger;
    
    public UnsafeProductService(SimpleDbContext context, ILogger<UnsafeProductService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    // Thread 1: Add initial product
    public async Task<Guid> AddProductAsync(string name, decimal price, int quantity)
    {
        var product = new SimpleProduct
        {
            ProductId = Guid.NewGuid(),
            Name = name,
            Price = price,
            Quantity = quantity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Product {ProductId} added by Thread 1", product.ProductId);
        return product.ProductId;
    }
    
    // Multiple threads: Unsafe update - WILL CAUSE LOST UPDATES
    public async Task<bool> UnsafeUpdateQuantityAsync(Guid productId, int quantityToAdd, int threadId)
    {
        try
        {
            // Step 1: Read current value
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);
            
            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found by Thread {ThreadId}", 
                    productId, threadId);
                return false;
            }
            
            var originalQuantity = product.Quantity;
            _logger.LogInformation("Thread {ThreadId} read current quantity: {CurrentQuantity}, adding: {QuantityToAdd}",
                threadId, originalQuantity, quantityToAdd);
            
            // Step 2: Simulate processing time - this increases the race condition window
            await Task.Delay(50);
            
            // Step 3: Modify the value (RACE CONDITION: other threads might have modified it)
            product.Quantity += quantityToAdd;
            
            _logger.LogInformation("Thread {ThreadId} calculated new quantity: {NewQuantity}",
                threadId, product.Quantity);
            
            // Step 4: Write back to database (LOST UPDATE PROBLEM)
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Thread {ThreadId} completed update for product {ProductId}",
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
    public async Task<SimpleProduct?> GetProductAsync(Guid productId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId);
    }
}

// Program.cs - Demonstrating lost updates without concurrency control
public class UnsafeProgram
{
    public static async Task Main(string[] args)
    {
        // Setup DI container and DbContext
        var services = new ServiceCollection()
            .AddDbContext<SimpleDbContext>(options =>
                options.UseSqlServer("YourConnectionString"))
            .AddScoped<UnsafeProductService>()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
            .BuildServiceProvider();
        
        using var scope = services.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<UnsafeProductService>();
        
        // Thread 1: Add initial product
        var productId = await productService.AddProductAsync("Unsafe Product", 99.99m, 10);
        
        Console.WriteLine("=== NO CONCURRENCY CONTROL (LOST UPDATES) ===");
        
        // Create 100 concurrent threads with NO concurrency control
        var unsafeTasks = Enumerable.Range(1, 100)
            .Select(threadId => Task.Run(async () =>
            {
                using var scope = services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<UnsafeProductService>();
                await service.UnsafeUpdateQuantityAsync(productId, 1, threadId);
            }))
            .ToArray();
        
        // Wait for all 100 threads to complete
        await Task.WhenAll(unsafeTasks);
        
        // Check result - this will almost certainly be less than expected
        var finalProduct = await productService.GetProductAsync(productId);
        Console.WriteLine($"Final quantity: {finalProduct?.Quantity}");
        Console.WriteLine($"Expected: 110 (10 initial + 100 additions), Actual: {finalProduct?.Quantity}");
        Console.WriteLine($"Lost updates: {110 - (finalProduct?.Quantity ?? 0)}");
        Console.WriteLine($"Result is deterministic: {(finalProduct?.Quantity == 110 ? "✓ YES" : "✗ NO - Lost updates occurred!")}");
        
        if (finalProduct?.Quantity < 110)
        {
            Console.WriteLine("🚨 This demonstrates why concurrency control is essential!");
        }
    }
}
```

### The Lost Update Problem

The above example demonstrates the classic **lost update problem**:

1. **Thread A** reads quantity: 10
2. **Thread B** reads quantity: 10 (same value)
3. **Thread A** calculates: 10 + 1 = 11
4. **Thread B** calculates: 10 + 1 = 11
5. **Thread A** writes: 11
6. **Thread B** writes: 11 (overwrites Thread A's change)

**Result**: Only one increment is preserved, the other is lost.

With 100 concurrent threads, this problem becomes severe, often resulting in far fewer updates than expected. This is why proper concurrency control mechanisms are essential.

## Optimistic Concurrency Control

Optimistic concurrency assumes that conflicts between concurrent operations are rare and detects conflicts only when attempting to commit changes. This approach uses versioning mechanisms to track changes and prevent lost updates.

### Table Definition with Version Timestamp

```sql
-- SQL Server table definition with optimistic concurrency
CREATE TABLE [dbo].[Products] (
    [ProductId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Name] NVARCHAR(200) NOT NULL,
    [Price] DECIMAL(18,2) NOT NULL,
    [Quantity] INT NOT NULL,
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [VersionTimestamp] ROWVERSION NOT NULL
);

-- Index for better performance on lookups
CREATE INDEX IX_Products_Name ON [dbo].[Products] ([Name]);
```

The `ROWVERSION` (also known as `TIMESTAMP` in older SQL Server versions) automatically increments whenever the row is modified, providing a built-in optimistic concurrency mechanism.

### Entity Framework Model and DbContext

```csharp
// Product.cs - Entity model
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Product
{
    public Guid ProductId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    
    public int Quantity { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    [Timestamp]
    public byte[] VersionTimestamp { get; set; } = Array.Empty<byte>();
}

// ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<Product> Products { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.VersionTimestamp).IsRowVersion();
        });
    }
    
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<Product>()
            .Where(e => e.State == EntityState.Modified);
            
        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

### Optimistic Concurrency Example with Retry Logic

```csharp
// ProductService.cs - Service with optimistic concurrency handling
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class ProductService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductService> _logger;
    
    public ProductService(ApplicationDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    // Thread 1: Add initial product
    public async Task<Guid> AddProductAsync(string name, decimal price, int quantity)
    {
        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            Name = name,
            Price = price,
            Quantity = quantity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Product {ProductId} added by Thread 1", product.ProductId);
        return product.ProductId;
    }
    
    // Threads 2 & 3: Update product with optimistic concurrency and retry
    public async Task<bool> UpdateProductQuantityAsync(Guid productId, int quantityToAdd, int threadId)
    {
        const int maxRetries = 3;
        int retryCount = 0;
        
        while (retryCount < maxRetries)
        {
            try
            {
                // Fetch the current product with its version timestamp
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.ProductId == productId);
                
                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found by Thread {ThreadId}", 
                        productId, threadId);
                    return false;
                }
                
                var originalQuantity = product.Quantity;
                _logger.LogInformation("Thread {ThreadId} attempting to add {QuantityToAdd} to current quantity {CurrentQuantity}",
                    threadId, quantityToAdd, originalQuantity);
                
                // Simulate some processing time to increase chance of concurrency conflict
                await Task.Delay(100);
                
                // Add to the product quantity
                product.Quantity += quantityToAdd;
                
                // This will throw DbUpdateConcurrencyException if another thread modified the row
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Thread {ThreadId} successfully added {QuantityAdded} to product {ProductId}, new quantity: {NewQuantity}",
                    threadId, quantityToAdd, productId, product.Quantity);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                retryCount++;
                _logger.LogWarning("Thread {ThreadId} encountered concurrency conflict on attempt {RetryCount}/{MaxRetries}. Exception: {Exception}",
                    threadId, retryCount, maxRetries, ex.Message);
                
                // Reset the context to clear the tracked entities
                foreach (var entry in _context.ChangeTracker.Entries())
                {
                    entry.Reload();
                }
                
                if (retryCount >= maxRetries)
                {
                    _logger.LogError("Thread {ThreadId} failed to update product {ProductId} after {MaxRetries} attempts",
                        threadId, productId, maxRetries);
                    return false;
                }
                
                // Add exponential backoff delay
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryCount - 1)));
            }
        }
        
        return false;
    }
}

// Program.cs - Example usage demonstrating concurrent updates
public class Program
{
    public static async Task Main(string[] args)
    {
        // Setup DI container and DbContext
        var services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer("YourConnectionString"))
            .AddScoped<ProductService>()
            .AddLogging(builder => builder.AddConsole())
            .BuildServiceProvider();
        
        using var scope = services.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<ProductService>();
        
        // Thread 1: Add initial product
        var productId = await productService.AddProductAsync("Sample Product", 99.99m, 10);
        
        // Create 100 concurrent threads to update the same product
        var concurrentTasks = Enumerable.Range(1, 100)
            .Select(threadId => Task.Run(async () =>
            {
                using var scope = services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ProductService>();
                await service.UpdateProductQuantityAsync(productId, 1, threadId);
            }))
            .ToArray();
        
        // Wait for all 100 threads to complete
        await Task.WhenAll(concurrentTasks);
    }
}
```

## Transaction-Based Concurrency Control

Transaction-based concurrency control uses explicit database transactions with isolation levels to manage concurrent access. This approach provides stronger consistency guarantees but may reduce concurrency due to locking.

### Transaction Management with Entity Framework

```csharp
// ProductTransactionService.cs - Service using explicit transactions
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;

public class ProductTransactionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductTransactionService> _logger;
    
    public ProductTransactionService(ApplicationDbContext context, ILogger<ProductTransactionService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    // Thread 1: Add initial product with transaction
    public async Task<Guid> AddProductWithTransactionAsync(string name, decimal price, int quantity)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        
        try
        {
            var product = new Product
            {
                ProductId = Guid.NewGuid(),
                Name = name,
                Price = price,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            
            await transaction.CommitAsync();
            
            _logger.LogInformation("Product {ProductId} added by Thread 1 with transaction", product.ProductId);
            return product.ProductId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to add product in Thread 1");
            throw;
        }
    }
    
    // Threads 2 & 3: Update product using transactions with retry logic
    public async Task<bool> UpdateProductQuantityWithTransactionAsync(Guid productId, int quantityToAdd, int threadId)
    {
        const int maxRetries = 3;
        int retryCount = 0;
        
        while (retryCount < maxRetries)
        {
            IDbContextTransaction? transaction = null;
            
            try
            {
                // Begin transaction with serializable isolation level for strong consistency
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                
                _logger.LogInformation("Thread {ThreadId} started transaction for product {ProductId}",
                    threadId, productId);
                
                // Fetch the current product within the transaction
                var product = await _context.Products
                    .Where(p => p.ProductId == productId)
                    .FirstOrDefaultAsync();
                
                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found by Thread {ThreadId}", 
                        productId, threadId);
                    await transaction.RollbackAsync();
                    return false;
                }
                
                var originalQuantity = product.Quantity;
                _logger.LogInformation("Thread {ThreadId} read product quantity: {CurrentQuantity}, adding: {QuantityToAdd}",
                    threadId, originalQuantity, quantityToAdd);
                
                // Simulate some processing time
                await Task.Delay(100);
                
                // Add to the product quantity
                product.Quantity += quantityToAdd;
                
                // Save changes within the transaction
                await _context.SaveChangesAsync();
                
                // Commit the transaction
                await transaction.CommitAsync();
                
                _logger.LogInformation("Thread {ThreadId} successfully committed transaction for product {ProductId}, added {QuantityAdded}, new quantity: {NewQuantity}",
                    threadId, productId, quantityToAdd, product.Quantity);
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("deadlock") || 
                                                      ex.Message.Contains("timeout") ||
                                                      ex.InnerException?.Message.Contains("deadlock") == true)
            {
                await transaction?.RollbackAsync()!;
                retryCount++;
                
                _logger.LogWarning("Thread {ThreadId} encountered deadlock/timeout on attempt {RetryCount}/{MaxRetries}. Exception: {Exception}",
                    threadId, retryCount, maxRetries, ex.Message);
                
                if (retryCount >= maxRetries)
                {
                    _logger.LogError("Thread {ThreadId} failed to update product {ProductId} after {MaxRetries} attempts due to deadlock/timeout",
                        threadId, productId, maxRetries);
                    return false;
                }
                
                // Add exponential backoff delay with jitter to reduce contention
                var delayMs = (int)(100 * Math.Pow(2, retryCount - 1) + Random.Shared.Next(0, 100));
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
                
                // Clear any tracked entities before retry
                _context.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                await transaction?.RollbackAsync()!;
                _logger.LogError(ex, "Thread {ThreadId} encountered unexpected error updating product {ProductId}",
                    threadId, productId);
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }
        
        return false;
    }
    
    // Method to demonstrate reading with different isolation levels
    public async Task<Product?> GetProductWithIsolationAsync(Guid productId, IsolationLevel isolationLevel)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(isolationLevel);
        
        try
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);
            
            await transaction.CommitAsync();
            return product;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

// Enhanced Program.cs demonstrating transaction-based concurrency
public class TransactionProgram
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer("YourConnectionString"))
            .AddScoped<ProductTransactionService>()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
            .BuildServiceProvider();
        
        using var scope = services.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<ProductTransactionService>();
        
        // Thread 1: Add initial product
        var productId = await productService.AddProductWithTransactionAsync("Transaction Product", 149.99m, 50);
        
        // Create 100 concurrent threads using transactions
        var transactionTasks = Enumerable.Range(1, 100)
            .Select(threadId => Task.Run(async () =>
            {
                using var scope = services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ProductTransactionService>();
                await service.UpdateProductQuantityWithTransactionAsync(productId, 1, threadId);
            }))
            .ToArray();
        
        // Wait for all 100 threads to complete
        await Task.WhenAll(transactionTasks);
        
        // Read final state
        using var finalScope = services.CreateScope();
        var finalService = finalScope.ServiceProvider.GetRequiredService<ProductTransactionService>();
        var finalProduct = await finalService.GetProductWithIsolationAsync(productId, IsolationLevel.ReadCommitted);
        
        Console.WriteLine($"Final product quantity: {finalProduct?.Quantity}");
    }
}
```

### Key Differences Between Approaches

| Aspect | Optimistic Concurrency | Transaction-Based Concurrency |
|--------|------------------------|-------------------------------|
| **Scope** | Per-row concurrency control | All modified rows across all tables |
| **Conflict Detection** | At commit time via version checks | During transaction via locks |
| **Performance** | Higher concurrency, lower latency | Lower concurrency, higher consistency |
| **Exception Type** | `DbUpdateConcurrencyException` | `InvalidOperationException` (deadlock/timeout) |
| **Retry Strategy** | Version-based retry with reload | Transaction retry with backoff |
| **Isolation** | Minimal locking | Configurable isolation levels |
| **Best For** | High-read, low-conflict scenarios | High-consistency requirements |

**Important Scope Distinction:**

- **Optimistic Concurrency**: Controls concurrency **per individual row**. Each entity with a version timestamp is protected independently. If you modify multiple entities in one SaveChanges call, each entity is checked for conflicts separately.

- **Transaction-Based Concurrency**: Controls concurrency for **all modifications within the transaction scope**. This includes all rows modified across all tables during the transaction. The isolation level applies to the entire transaction, providing consistency guarantees for the complete set of operations.

Both approaches have their place in modern applications. Optimistic concurrency is ideal for scenarios with infrequent conflicts and high read-to-write ratios, while transaction-based concurrency is better suited for scenarios requiring strong consistency guarantees and complex multi-table operations.

---

**Navigation:**

- Previous: [Concurrency and databases](./concurrency-and-dbs.md)
- Next: [Concurrency and document databases](./concurrency-document-dbs.md)
