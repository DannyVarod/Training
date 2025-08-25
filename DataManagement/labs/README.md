# Concurrency Labs

This directory contains runnable C# examples demonstrating concurrency concepts covered in the DataManagement course materials.

## Projects Overview

### 1. ConcurrencyFundamentals
**File**: `ConcurrencyFundamentals/ConcurrencyFundamentalsTests.cs`

Demonstrates fundamental concurrency concepts in C#:
- Race conditions and their effects
- Synchronization primitives (locks, mutexes, semaphores)
- Atomic operations and Compare-and-Exchange
- Lock-free data structures
- Concurrent collections
- Deadlock prevention techniques

**Requirements**: .NET 8.0 (no external dependencies)

### 2. RelationalConcurrency
**File**: `RelationalConcurrency/RelationalConcurrencyTests.cs`

Demonstrates concurrency control in relational databases using Entity Framework Core:
- No concurrency control (demonstrates lost updates)
- Optimistic concurrency control with version fields
- Transactional concurrency control
- Pessimistic locking patterns
- Multi-account transaction scenarios

**Requirements**: .NET 8.0, Entity Framework Core (uses in-memory database for testing)

### 3. DocumentConcurrency
**File**: `DocumentConcurrency/DocumentConcurrencyTests.cs`

Demonstrates MongoDB atomic operations and concurrency patterns:
- Document-level atomic operations
- Complex multi-field updates
- Conditional updates with filters
- Array field atomic operations
- Upsert operations

**Requirements**: .NET 8.0, MongoDB.Driver (optionally MongoDB instance)

## Running the Examples

### Prerequisites

1. **.NET 8.0 SDK** - Download from [Microsoft .NET](https://dotnet.microsoft.com/download)
2. **Visual Studio 2022** or **VS Code** with C# extension

### Option 1: Run All Tests

```bash
# Navigate to the labs directory
cd DataManagement/labs

# Restore packages and build solution
dotnet restore
dotnet build

# Run all tests
dotnet test
```

### Option 2: Run Individual Projects

```bash
# Run only ConcurrencyFundamentals tests
dotnet test ConcurrencyFundamentals

# Run only RelationalConcurrency tests  
dotnet test RelationalConcurrency

# Run only DocumentConcurrency tests (requires MongoDB)
dotnet test DocumentConcurrency
```

### Option 3: Run in Visual Studio

1. Open `Concurrency.sln` in Visual Studio 2022
2. Build the solution (Build → Build Solution)
3. Run tests using Test Explorer (Test → Test Explorer)

## MongoDB Setup (for DocumentConcurrency)

The DocumentConcurrency project can run without MongoDB by modifying the connection logic, but for full functionality:

### Option 1: Docker (Recommended)

```bash
# Start MongoDB container
docker run -d -p 27017:27017 --name mongodb mongo:latest

# Run tests
dotnet test DocumentConcurrency
```

### Option 2: Local Installation

1. Install [MongoDB Community Edition](https://www.mongodb.com/try/download/community)
2. Start MongoDB service
3. Run tests

### Option 3: MongoDB Atlas (Cloud)

1. Create free cluster at [MongoDB Atlas](https://www.mongodb.com/atlas)
2. Update connection string in `DocumentConcurrencyTests.cs`
3. Run tests

## Expected Behaviors

### ConcurrencyFundamentals
- **RaceConditionDemo**: Should show inconsistent results (counter < expected)
- **Synchronization tests**: Should show consistent results (counter = expected)
- **LockFreeStackDemo**: Should successfully push/pop all items
- **ConcurrentCollections**: Should handle concurrent operations safely

### RelationalConcurrency
- **NoConcurrencyControl**: May show lost updates (non-deterministic)
- **OptimisticConcurrencyControl**: Should handle all updates correctly with retries
- **TransactionalConcurrency**: Should maintain data consistency
- **BankTransfer**: Should preserve total money in system

### DocumentConcurrency
- **NoConcurrencyControl**: May show lost updates with ReplaceOneAsync
- **AtomicOperations**: Should handle all updates correctly using $inc operators
- **ConditionalUpdates**: Should respect balance constraints
- **ArrayOperations**: Should safely add transactions to arrays

## Learning Objectives

After running these examples, you should understand:

1. **Why concurrency control matters**: See actual lost updates in action
2. **Different approaches**: Compare optimistic vs pessimistic vs atomic operations
3. **Database-specific patterns**: How SQL and NoSQL handle concurrency differently
4. **Real-world scenarios**: Bank transfers, user sessions, transaction logging
5. **Performance trade-offs**: Atomic operations vs locks vs retry logic

## Troubleshooting

### Common Issues

**Package Restore Errors**:
```bash
dotnet clean
dotnet restore
dotnet build
```

**MongoDB Connection Errors**:
- Ensure MongoDB is running on localhost:27017
- Or modify connection string in DocumentConcurrencyTests.cs
- Or comment out MongoDB tests temporarily

**Entity Framework Errors**:
- The RelationalConcurrency project uses in-memory database, no SQL Server required
- If issues persist, try: `dotnet add package Microsoft.EntityFrameworkCore.InMemory`

**Test Discovery Issues**:
```bash
# Rebuild and re-run test discovery
dotnet build
dotnet test --list-tests
```

### Performance Notes

- Tests use reduced iteration counts (100-200) for faster execution
- In production scenarios, these numbers would be much higher
- Some tests include intentional delays to increase concurrency likelihood
- Results may vary between test runs, especially for race condition demonstrations

## Integration with Course Materials

These labs directly correspond to:
- **ConcurrencyFundamentals** → `concurrency-fundamentals-csharp.md`
- **RelationalConcurrency** → `concurrency-relational-dbs.md`  
- **DocumentConcurrency** → `concurrency-document-dbs.md`

Run the examples while reading the corresponding documentation for maximum learning impact.
