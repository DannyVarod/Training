using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ConcurrencyFundamentals;

[TestClass]
public class ConcurrencyFundamentalsTests
{
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
        
        // Instead, let's verify that we have a race condition
        Assert.IsTrue(counter < iterations, "Race condition should cause lost updates");
    }

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
                    await Task.Delay(100); // Simulate work
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

    [TestMethod]
    public void DeadlockPreventionDemo()
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

    [TestMethod]
    public void ConcurrentDictionaryDemo()
    {
        var concurrentDict = new ConcurrentDictionary<string, int>();
        int iterations = 1000;

        // Multiple threads adding and updating values concurrently
        Parallel.For(0, iterations, i =>
        {
            var key = $"key_{i % 100}"; // Create some key collisions intentionally
            
            // AddOrUpdate is atomic - either adds new key or updates existing
            concurrentDict.AddOrUpdate(
                key, 
                addValue: 1,                    // Value to add if key doesn't exist
                updateValueFactory: (k, v) => v + 1  // Function to update if key exists
            );
        });

        // Verify thread-safe operations worked correctly
        int totalValue = concurrentDict.Values.Sum();
        Console.WriteLine($"Total value across all keys: {totalValue}");
        Assert.AreEqual(iterations, totalValue);
        
        Console.WriteLine($"Dictionary contains {concurrentDict.Count} items");
    }

    [TestMethod]
    public void ConcurrentQueueAndStackDemo()
    {
        var concurrentQueue = new ConcurrentQueue<int>();
        var concurrentStack = new ConcurrentStack<int>();
        int itemCount = 1000;

        // Producer threads adding items
        var producers = Enumerable.Range(0, 3).Select(producerId => Task.Run(() =>
        {
            for (int i = 0; i < itemCount / 3; i++)
            {
                int value = producerId * 1000 + i;
                concurrentQueue.Enqueue(value);
                concurrentStack.Push(value);
            }
        }));

        // Wait for all producers to finish
        Task.WaitAll(producers.ToArray());

        // Consumer threads removing items
        int queueItemsProcessed = 0;
        int stackItemsProcessed = 0;
        
        var consumers = Enumerable.Range(0, 2).Select(consumerId => Task.Run(() =>
        {
            // Process queue items
            while (concurrentQueue.TryDequeue(out int queueItem))
            {
                Interlocked.Increment(ref queueItemsProcessed);
            }
            
            // Process stack items  
            while (concurrentStack.TryPop(out int stackItem))
            {
                Interlocked.Increment(ref stackItemsProcessed);
            }
        }));

        Task.WaitAll(consumers.ToArray());

        Console.WriteLine($"Queue items processed: {queueItemsProcessed}");
        Console.WriteLine($"Stack items processed: {stackItemsProcessed}");
        Assert.AreEqual(itemCount, queueItemsProcessed);
        Assert.AreEqual(itemCount, stackItemsProcessed);
    }

    [TestMethod]
    public void BlockingCollectionDemo()
    {
        using (var blockingCollection = new BlockingCollection<int>(boundedCapacity: 10))
        {
            int itemsToProduce = 50;
            int itemsProduced = 0;
            int itemsConsumed = 0;

            // Producer task
            var producer = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < itemsToProduce; i++)
                    {
                        blockingCollection.Add(i); // Blocks if collection is full
                        Interlocked.Increment(ref itemsProduced);
                        Console.WriteLine($"Produced: {i}");
                    }
                }
                finally
                {
                    blockingCollection.CompleteAdding(); // Signal no more items
                }
            });

            // Consumer task
            var consumer = Task.Run(() =>
            {
                try
                {
                    // GetConsumingEnumerable blocks until items are available
                    foreach (int item in blockingCollection.GetConsumingEnumerable())
                    {
                        Thread.Sleep(10); // Simulate processing time
                        Interlocked.Increment(ref itemsConsumed);
                        Console.WriteLine($"Consumed: {item}");
                    }
                }
                catch (InvalidOperationException)
                {
                    // Collection was marked as complete
                }
            });

            Task.WaitAll(producer, consumer);
            
            Assert.AreEqual(itemsToProduce, itemsProduced);
            Assert.AreEqual(itemsToProduce, itemsConsumed);
            Console.WriteLine($"Produced: {itemsProduced}, Consumed: {itemsConsumed}");
        }
    }

    [TestMethod]
    public void ImmutableCollectionsDemo()
    {
        // Start with empty immutable list
        var originalList = ImmutableList<string>.Empty;
        
        // "Adding" to immutable collections returns new instances
        var list1 = originalList.Add("Item1");
        var list2 = list1.Add("Item2").Add("Item3");
        
        Console.WriteLine($"Original: {originalList.Count} items");
        Console.WriteLine($"List1: {list1.Count} items");
        Console.WriteLine($"List2: {list2.Count} items");
        
        // Concurrent operations on immutable collections
        var results = new ConcurrentBag<ImmutableList<int>>();
        
        Parallel.For(0, 100, i =>
        {
            var numbers = ImmutableList<int>.Empty;
            
            // Build list for this thread
            for (int j = 0; j < 10; j++)
            {
                numbers = numbers.Add(i * 10 + j);
            }
            
            results.Add(numbers);
        });
        
        // All lists are independent and thread-safe
        Assert.AreEqual(100, results.Count);
        Assert.IsTrue(results.All(list => list.Count == 10));
        
        Console.WriteLine($"Created {results.Count} independent immutable lists");
    }
}

// Lock-free stack implementation using Compare and Exchange
public class LockFreeStack<T>
{
    private class Node
    {
        public T Value;
        public Node? Next;

        public Node(T value)
        {
            Value = value;
        }
    }

    private Node? _head;

    public void Push(T item)
    {
        var newNode = new Node(item);
        
        do
        {
            newNode.Next = _head; // Point new node to current head
            
            // Atomically update head only if it hasn't changed
            // If another thread modified _head, retry with the new head value
        } while (Interlocked.CompareExchange(ref _head, newNode, newNode.Next) != newNode.Next);
    }

    public bool TryPop(out T? result)
    {
        Node? currentHead;
        
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

// Simple spin lock implementation using Compare and Exchange
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
