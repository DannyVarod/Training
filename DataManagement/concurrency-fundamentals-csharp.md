# Concurrency Fundamentals for C# Developers

Before diving into database-specific concurrency mechanisms, it's essential to understand fundamental concurrency concepts in C#. This section demonstrates race conditions, synchronization primitives, and best practices for concurrent programming.

## Race Conditions

### The Problem: Non-Deterministic Results

```csharp
[TestMethod]
public void RaceConditionDemo()
{
    int counter = 0;
    int iterations = 1000;

    // This will produce non-deterministic results
    Parallel.For(0, iterations, i =>
    {
        counter++; // Race condition: read-modify-write operation is not atomic
    });

    // This assertion will frequently fail because counter < iterations
    // The exact value will vary between test runs
    Console.WriteLine($"Expected: {iterations}, Actual: {counter}");
    
    // Uncomment to see the race condition in action:
    // Assert.AreEqual(iterations, counter); // This will fail!
}
```

**Why this fails:**

- Multiple threads read the same value of `counter`
- They increment it simultaneously
- Some increments are lost when threads overwrite each other's changes
- Result is non-deterministic and typically less than expected

## Synchronization Strategies

### 1. Mutex (Cross-Process Synchronization)

```csharp
[TestMethod]
public void MutexDemo()
{
    int counter = 0;
    int iterations = 1000;
    
    using (var mutex = new Mutex(false, "MyAppCounterMutex"))
    {
        Parallel.For(0, iterations, i =>
        {
            mutex.WaitOne(); // Acquire mutex
            try
            {
                counter++; // Protected critical section
            }
            finally
            {
                mutex.ReleaseMutex(); // Always release in finally block
            }
        });
    }

    Assert.AreEqual(iterations, counter); // This will pass
    Console.WriteLine($"Expected: {iterations}, Actual: {counter}");
}
```

### 2. Lock Statement (Intra-Process Synchronization)

```csharp
[TestMethod]
public void LockDemo()
{
    int counter = 0;
    int iterations = 1000;
    object lockObject = new object(); // Dedicated lock object

    Parallel.For(0, iterations, i =>
    {
        lock (lockObject) // Acquire lock
        {
            counter++; // Protected critical section
        } // Lock automatically released
    });

    Assert.AreEqual(iterations, counter); // This will pass
    Console.WriteLine($"Expected: {iterations}, Actual: {counter}");
}
```

### 3. Atomic Operations (Best Performance)

```csharp
[TestMethod]
public void AtomicIncrementDemo()
{
    int counter = 0;
    int iterations = 1000;

    Parallel.For(0, iterations, i =>
    {
        Interlocked.Increment(ref counter); // Atomic increment operation
    });

    Assert.AreEqual(iterations, counter); // This will pass
    Console.WriteLine($"Expected: {iterations}, Actual: {counter}");
}
```

### 4. Atomic Compare and Exchange (Advanced)

Compare and Exchange (CAS) is a fundamental atomic operation that enables lock-free programming by atomically comparing a value with an expected value and, if they match, replacing it with a new value.

```csharp
[TestMethod]
public void CompareExchangeDemo()
{
    int sharedValue = 0;
    int iterations = 1000;
    int successfulUpdates = 0;

    Parallel.For(0, iterations, i =>
    {
        int currentValue, newValue;
        do
        {
            currentValue = sharedValue; // Read current value
            newValue = currentValue + 1; // Calculate new value
            
            // Atomically update only if value hasn't changed since we read it
            // Returns the actual value that was in sharedValue before the operation
        } while (Interlocked.CompareExchange(ref sharedValue, newValue, currentValue) != currentValue);
        
        // If we get here, our update succeeded
        Interlocked.Increment(ref successfulUpdates);
    });

    Assert.AreEqual(iterations, sharedValue); // This will pass
    Assert.AreEqual(iterations, successfulUpdates); // This will pass
    Console.WriteLine($"Expected: {iterations}, Actual: {sharedValue}, Successful Updates: {successfulUpdates}");
}
```

#### Advanced Example: Lock-Free Stack Implementation

```csharp
public class LockFreeStack<T>
{
    private class Node
    {
        public T Value;
        public Node Next;
    }

    private Node _head;

    public void Push(T item)
    {
        var newNode = new Node { Value = item };
        
        do
        {
            newNode.Next = _head; // Point new node to current head
            
            // Atomically update head only if it hasn't changed
            // If another thread modified _head, retry with the new head value
        } while (Interlocked.CompareExchange(ref _head, newNode, newNode.Next) != newNode.Next);
    }

    public bool TryPop(out T result)
    {
        Node currentHead;
        
        do
        {
            currentHead = _head;
            if (currentHead == null)
            {
                result = default(T);
                return false; // Stack is empty
            }
            
            // Try to update head to point to next node
            // If another thread modified _head, retry
        } while (Interlocked.CompareExchange(ref _head, currentHead.Next, currentHead) != currentHead);

        result = currentHead.Value;
        return true;
    }
}

[TestMethod]
public void LockFreeStackDemo()
{
    var stack = new LockFreeStack<int>();
    int pushCount = 1000;
    int popCount = 0;
    
    // Push items concurrently
    Parallel.For(0, pushCount, i =>
    {
        stack.Push(i);
    });
    
    // Pop items concurrently
    Parallel.For(0, pushCount, i =>
    {
        if (stack.TryPop(out int value))
        {
            Interlocked.Increment(ref popCount);
        }
    });
    
    Assert.AreEqual(pushCount, popCount); // All items should be popped
    
    // Verify stack is empty
    Assert.IsFalse(stack.TryPop(out int remainingValue));
    Console.WriteLine($"Successfully pushed and popped {popCount} items using lock-free operations");
}
```

#### Compare and Exchange vs. Simple Atomic Operations

| Operation | Use Case | Performance | Complexity | Retry Logic |
|-----------|----------|-------------|------------|-------------|
| **Interlocked.Increment** | Simple counters | Fastest | Simplest | Not needed |
| **Interlocked.Exchange** | Simple value replacement | Very Fast | Simple | Not needed |
| **Interlocked.CompareExchange** | Conditional updates, lock-free data structures | Fast | Complex | Manual retry loops |

#### When to Use Compare and Exchange

- **Lock-free data structures**: Stacks, queues, linked lists
- **Optimistic updates**: When you want to retry on conflict rather than block
- **Performance-critical sections**: When locks would be too slow
- **ABA problem prevention**: When you need to detect if a value changed and changed back

#### Best Practices for Compare and Exchange

```csharp
// Good: Clear retry loop with proper logic
int currentValue, newValue;
do
{
    currentValue = sharedVariable;
    newValue = CalculateNewValue(currentValue);
} while (Interlocked.CompareExchange(ref sharedVariable, newValue, currentValue) != currentValue);

// Bad: Incorrect comparison (common mistake)
do
{
    currentValue = sharedVariable;
    newValue = CalculateNewValue(currentValue);
} while (!Interlocked.CompareExchange(ref sharedVariable, newValue, currentValue)); // WRONG!
```

## Semaphore: Controlling Resource Access

Semaphores limit the number of threads that can access a resource simultaneously.

```csharp
[TestMethod]
public async Task SemaphoreDemo()
{
    const int maxConcurrentWorkers = 3;
    const int totalTasks = 10;
    int completedTasks = 0;
    
    using (var semaphore = new SemaphoreSlim(maxConcurrentWorkers, maxConcurrentWorkers))
    {
        var tasks = Enumerable.Range(0, totalTasks).Select(async taskId =>
        {
            await semaphore.WaitAsync(); // Wait for available slot
            try
            {
                // Simulate work (only 3 workers can be here at once)
                Console.WriteLine($"Task {taskId} started at {DateTime.Now:HH:mm:ss.fff}");
                await Task.Delay(1000); // Simulate 1 second of work
                Console.WriteLine($"Task {taskId} completed at {DateTime.Now:HH:mm:ss.fff}");
                
                Interlocked.Increment(ref completedTasks);
            }
            finally
            {
                semaphore.Release(); // Always release the semaphore
            }
        });

        await Task.WhenAll(tasks);
    }

    Assert.AreEqual(totalTasks, completedTasks);
    Console.WriteLine($"All {totalTasks} tasks completed with max {maxConcurrentWorkers} concurrent workers");
}
```

## Deadlocks and Prevention

### The Deadlock Problem

```csharp
// DON'T DO THIS - Can cause deadlocks!
[TestMethod]
public void DeadlockExample()
{
    object lockA = new object();
    object lockB = new object();
    bool deadlockOccurred = false;

    var task1 = Task.Run(() =>
    {
        try
        {
            lock (lockA)
            {
                Thread.Sleep(100); // Increase chance of deadlock
                lock (lockB)
                {
                    Console.WriteLine("Task 1: Acquired both locks");
                }
            }
        }
        catch (Exception)
        {
            deadlockOccurred = true;
        }
    });

    var task2 = Task.Run(() =>
    {
        try
        {
            lock (lockB) // Different order - potential deadlock!
            {
                Thread.Sleep(100); // Increase chance of deadlock
                lock (lockA)
                {
                    Console.WriteLine("Task 2: Acquired both locks");
                }
            }
        }
        catch (Exception)
        {
            deadlockOccurred = true;
        }
    });

    // Use timeout to detect potential deadlock
    bool completed = Task.WaitAll(new[] { task1, task2 }, TimeSpan.FromSeconds(5));
    
    if (!completed)
    {
        Console.WriteLine("Potential deadlock detected - tasks didn't complete in time");
    }
}
```

### Deadlock Prevention: Consistent Lock Ordering

```csharp
[TestMethod]
public void DeadlockPrevention()
{
    object lockA = new object();
    object lockB = new object();
    
    // Always acquire locks in the same order: A before B
    var task1 = Task.Run(() =>
    {
        lock (lockA) // Always lock A first
        {
            Thread.Sleep(100);
            lock (lockB) // Then lock B
            {
                Console.WriteLine("Task 1: Acquired both locks safely");
            }
        }
    });

    var task2 = Task.Run(() =>
    {
        lock (lockA) // Always lock A first (same order!)
        {
            Thread.Sleep(100);
            lock (lockB) // Then lock B
            {
                Console.WriteLine("Task 2: Acquired both locks safely");
            }
        }
    });

    bool completed = Task.WaitAll(new[] { task1, task2 }, TimeSpan.FromSeconds(5));
    Assert.IsTrue(completed, "Tasks should complete without deadlock");
}
```

### Advanced Deadlock Prevention: Lock Ordering by Hash Code

```csharp
[TestMethod]
public void DeadlockPreventionWithDynamicOrdering()
{
    object lockA = new object();
    object lockB = new object();

    void AcquireLocksInOrder(object lock1, object lock2, string taskName)
    {
        // Always acquire locks in consistent order based on hash code
        object firstLock, secondLock;
        if (lock1.GetHashCode() < lock2.GetHashCode())
        {
            firstLock = lock1;
            secondLock = lock2;
        }
        else
        {
            firstLock = lock2;
            secondLock = lock1;
        }

        lock (firstLock)
        {
            Thread.Sleep(100);
            lock (secondLock)
            {
                Console.WriteLine($"{taskName}: Acquired both locks safely using hash code ordering");
            }
        }
    }

    var task1 = Task.Run(() => AcquireLocksInOrder(lockA, lockB, "Task 1"));
    var task2 = Task.Run(() => AcquireLocksInOrder(lockB, lockA, "Task 2")); // Different parameter order, but same lock order

    bool completed = Task.WaitAll(new[] { task1, task2 }, TimeSpan.FromSeconds(5));
    Assert.IsTrue(completed, "Tasks should complete without deadlock using consistent ordering");
}
```

## Performance Comparison

### Comparison of Synchronization Approaches

| Approach | Use Case | Performance | Complexity | Cross-Process |
|----------|----------|-------------|------------|---------------|
| **No Synchronization** | Single-threaded only | Fastest | Simplest | N/A |
| **Atomic Operations** | Simple operations (increment, exchange) | Very Fast | Simple | No |
| **Lock Statement** | General critical sections | Fast | Medium | No |
| **Mutex** | Cross-process synchronization | Slower | Medium | Yes |
| **Semaphore** | Resource limiting | Medium | Medium | Yes (SemaphoreSlim: No) |

### When to Use Each Approach

#### Atomic Operations

- **Use for**: Simple operations like counters, flags, simple data exchanges
- **Benefits**: Lock-free, highest performance, no deadlock risk
- **Limitations**: Limited to specific operations supported by `Interlocked` class

#### Lock Statement

- **Use for**: General critical sections within a single process
- **Benefits**: Easy to use, automatic cleanup, good performance
- **Limitations**: Intra-process only, potential for deadlocks

#### Mutex

- **Use for**: Cross-process synchronization, single-instance applications
- **Benefits**: Works across process boundaries
- **Limitations**: Slower than locks, more complex error handling

#### Semaphore

- **Use for**: Limiting concurrent access to resources (e.g., connection pools, file handles)
- **Benefits**: Controls resource usage, prevents resource exhaustion
- **Limitations**: More complex than simple locks, potential for resource leaks

## Best Practices

### 1. Choose the Right Tool

```csharp
// Good: Use atomic operations for simple counters
Interlocked.Increment(ref counter);

// Bad: Using locks for simple increment
lock (lockObject) { counter++; }
```

### 2. Minimize Lock Scope

```csharp
// Good: Minimal lock scope
lock (lockObject)
{
    sharedResource++;
}
DoExpensiveWork(); // Outside the lock

// Bad: Lock held too long
lock (lockObject)
{
    sharedResource++;
    DoExpensiveWork(); // Expensive work inside lock
}
```

### 3. Always Use Try-Finally for Manual Lock Management

```csharp
// Good: Proper cleanup
mutex.WaitOne();
try
{
    // Critical section
}
finally
{
    mutex.ReleaseMutex();
}

// Bad: Risk of not releasing
mutex.WaitOne();
// Critical section
mutex.ReleaseMutex(); // Might not execute if exception occurs
```

### 4. Consistent Lock Ordering

```csharp
// Good: Consistent ordering prevents deadlocks
void TransferMoney(Account from, Account to, decimal amount)
{
    Account firstLock = from.Id < to.Id ? from : to;
    Account secondLock = from.Id < to.Id ? to : from;
    
    lock (firstLock)
    {
        lock (secondLock)
        {
            from.Balance -= amount;
            to.Balance += amount;
        }
    }
}
```

## Advanced: Custom Spin Lock with Compare and Exchange

A spin lock is a low-level synchronization primitive that causes threads to wait in a loop ("spin") rather than being blocked. This can be more efficient than traditional locks when the critical section is very short and contention is low.

### Spin Lock Implementation

```csharp
public struct SimpleSpinLock
{
    private int _isLocked; // 0 = unlocked, 1 = locked

    public void Enter()
    {
        // Spin until we can atomically change _isLocked from 0 to 1
        while (Interlocked.CompareExchange(ref _isLocked, 1, 0) != 0)
        {
            // Spin - keep trying until the lock is available
            // Thread.Yield() can improve performance by allowing other threads to run
            Thread.Yield();
        }
    }

    public void Exit()
    {
        // Atomically release the lock by setting it back to 0
        Interlocked.Exchange(ref _isLocked, 0);
    }

    public bool IsLocked => _isLocked == 1;
}
```

### Proper Usage with Try-Finally

```csharp
[TestMethod]
public void SpinLockDemo()
{
    var spinLock = new SimpleSpinLock();
    int counter = 0;
    int iterations = 1000;

    Parallel.For(0, iterations, i =>
    {
        spinLock.Enter();
        try
        {
            // Critical section - very short operation
            counter++;
            
            // Simulate very brief work (spin locks are for SHORT critical sections only)
            Thread.SpinWait(10); // Spin for a few CPU cycles
        }
        finally
        {
            spinLock.Exit(); // CRITICAL: Always release the lock
        }
    });

    Assert.AreEqual(iterations, counter);
    Console.WriteLine($"SpinLock: Expected: {iterations}, Actual: {counter}");
}
```

### Enhanced Spin Lock with Timeout

```csharp
public struct SpinLockWithTimeout
{
    private int _isLocked;

    public bool TryEnter(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        
        while (Interlocked.CompareExchange(ref _isLocked, 1, 0) != 0)
        {
            if (stopwatch.Elapsed > timeout)
                return false; // Timeout reached
                
            Thread.Yield();
        }
        
        return true; // Successfully acquired the lock
    }

    public void Enter()
    {
        while (Interlocked.CompareExchange(ref _isLocked, 1, 0) != 0)
        {
            Thread.Yield();
        }
    }

    public void Exit()
    {
        Interlocked.Exchange(ref _isLocked, 0);
    }
}

[TestMethod]
public void SpinLockWithTimeoutDemo()
{
    var spinLock = new SpinLockWithTimeout();
    int successfulAcquisitions = 0;
    int timeouts = 0;

    // One thread holds the lock for a long time
    var longRunningTask = Task.Run(() =>
    {
        spinLock.Enter();
        try
        {
            Thread.Sleep(2000); // Hold lock for 2 seconds
        }
        finally
        {
            spinLock.Exit();
        }
    });

    // Other threads try to acquire with timeout
    var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
    {
        if (spinLock.TryEnter(TimeSpan.FromMilliseconds(100)))
        {
            try
            {
                Interlocked.Increment(ref successfulAcquisitions);
                Thread.Sleep(50);
            }
            finally
            {
                spinLock.Exit();
            }
        }
        else
        {
            Interlocked.Increment(ref timeouts);
        }
    }));

    Task.WaitAll(tasks.Concat(new[] { longRunningTask }).ToArray());

    Console.WriteLine($"Successful acquisitions: {successfulAcquisitions}, Timeouts: {timeouts}");
    Assert.IsTrue(timeouts > 0, "Some threads should have timed out");
}
```

### Performance Comparison: SpinLock vs Lock vs Mutex

```csharp
[TestMethod]
public void SynchronizationPerformanceComparison()
{
    const int iterations = 100000;
    int counter = 0;

    // Test SpinLock
    var spinLock = new SimpleSpinLock();
    var stopwatch = Stopwatch.StartNew();
    
    Parallel.For(0, iterations, i =>
    {
        spinLock.Enter();
        try
        {
            counter++;
        }
        finally
        {
            spinLock.Exit();
        }
    });
    
    var spinLockTime = stopwatch.Elapsed;
    Console.WriteLine($"SpinLock: {spinLockTime.TotalMilliseconds:F2}ms, Counter: {counter}");

    // Reset for next test
    counter = 0;
    
    // Test regular lock
    object lockObj = new object();
    stopwatch.Restart();
    
    Parallel.For(0, iterations, i =>
    {
        lock (lockObj)
        {
            counter++;
        }
    });
    
    var lockTime = stopwatch.Elapsed;
    Console.WriteLine($"Lock: {lockTime.TotalMilliseconds:F2}ms, Counter: {counter}");

    // Results will vary, but typically:
    // - SpinLock is faster for very short critical sections with low contention
    // - Regular lock is better for longer critical sections or high contention
}
```

### Common Mistakes and Best Practices

#### ❌ WRONG: Forgetting to release the lock

```csharp
// DON'T DO THIS - Can cause permanent deadlock!
public void BadSpinLockUsage()
{
    var spinLock = new SimpleSpinLock();
    
    spinLock.Enter();
    // If an exception occurs here, the lock is never released!
    DoSomeWork();
    spinLock.Exit(); // This might never execute
}
```

#### ✅ CORRECT: Always use try-finally

```csharp
public void GoodSpinLockUsage()
{
    var spinLock = new SimpleSpinLock();
    
    spinLock.Enter();
    try
    {
        DoSomeWork(); // Even if this throws, the lock will be released
    }
    finally
    {
        spinLock.Exit(); // This ALWAYS executes
    }
}
```

#### ❌ WRONG: Using SpinLock for long operations

```csharp
// DON'T DO THIS - SpinLocks waste CPU cycles
public void BadSpinLockForLongOperation()
{
    var spinLock = new SimpleSpinLock();
    
    Parallel.For(0, 100, i =>
    {
        spinLock.Enter();
        try
        {
            Thread.Sleep(100); // Long operation - other threads waste CPU spinning!
        }
        finally
        {
            spinLock.Exit();
        }
    });
}
```

#### ✅ CORRECT: Use regular locks for longer operations

```csharp
public void GoodLockForLongOperation()
{
    object lockObj = new object();
    
    Parallel.For(0, 100, i =>
    {
        lock (lockObj) // Threads are blocked, not spinning - CPU efficient
        {
            Thread.Sleep(100); // Longer operation
        }
    });
}
```

### When to Use SpinLocks

| Scenario | Use SpinLock | Use Regular Lock | Reason |
|----------|-------------|------------------|---------|
| **Very short critical sections** (< 100 CPU cycles) | ✅ Yes | ❌ No | Context switching overhead is higher than spinning |
| **Low contention** (few threads) | ✅ Yes | ✅ Either | Both work well, SpinLock may be slightly faster |
| **High contention** (many threads) | ❌ No | ✅ Yes | Too much CPU wasted on spinning |
| **Long critical sections** (> 1000 CPU cycles) | ❌ No | ✅ Yes | CPU wasted on spinning |
| **I/O operations** in critical section | ❌ No | ✅ Yes | I/O is inherently slow |
| **Memory allocation** in critical section | ❌ No | ✅ Yes | Allocation can be slow and unpredictable |

### SpinLock vs .NET's Built-in SpinLock

```csharp
[TestMethod]
public void BuiltInSpinLockComparison()
{
    // .NET provides a built-in SpinLock struct with additional features
    var builtInSpinLock = new SpinLock(enableThreadOwnerTracking: false);
    int counter = 0;
    int iterations = 1000;

    Parallel.For(0, iterations, i =>
    {
        bool lockTaken = false;
        try
        {
            builtInSpinLock.Enter(ref lockTaken);
            counter++;
        }
        finally
        {
            if (lockTaken)
                builtInSpinLock.Exit();
        }
    });

    Assert.AreEqual(iterations, counter);
    Console.WriteLine($"Built-in SpinLock: Counter = {counter}");
    
    // Built-in SpinLock advantages:
    // - Better performance optimizations
    // - Thread ownership tracking (optional)
    // - Better handling of thread aborts
    // - Adaptive spinning (falls back to blocking after a while)
}
```

### Key Takeaways

1. **SpinLocks are for very short critical sections only** - typically just a few CPU cycles
2. **Always use try-finally** to guarantee lock release, even if exceptions occur
3. **Compare and Exchange enables lock-free implementation** of synchronization primitives
4. **SpinLocks waste CPU cycles** - use regular locks for longer operations or high contention
5. **Consider .NET's built-in SpinLock** for production code - it has better optimizations

SpinLocks demonstrate the power of atomic operations but should be used sparingly and only when the performance benefits clearly outweigh the complexity.

## Connection to Database Concurrency

Understanding these fundamental concepts is crucial for database concurrency:

- **Race Conditions** in databases manifest as lost updates, dirty reads, and phantom reads
- **Atomic Operations** are provided by databases through transactions and atomic update operators
- **Lock Management** in databases uses similar principles but at the row/table/database level
- **Deadlock Prevention** applies to database transactions through consistent ordering and timeouts

The next sections will show how these concepts apply specifically to different database technologies, with each database type providing its own mechanisms for handling concurrent access safely and efficiently.

---

**Navigation:**

- Next: [NoSQL Fundamentals for SQL Developers](./nosql-fundamentals.md)
