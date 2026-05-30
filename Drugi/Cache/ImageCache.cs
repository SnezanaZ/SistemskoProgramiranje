using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
public class ImageCache
{
    private readonly int capacity;
    private readonly int cleanThreshold;

    private readonly CancellationTokenSource cts = new();

    // Gotovi podaci
    private readonly ConcurrentDictionary<string, byte[]> cache = new();

    // LRU strukture
    private readonly LinkedList<string> lru = new();
    private readonly Dictionary<string, LinkedListNode<string>> nodes = new();

    // lock za LRU
    private readonly object lruLock = new();

    // lock po fajlu
    private readonly ConcurrentDictionary<string, object> fileLocks = new();

    // cache stampede
    private readonly ConcurrentDictionary<string, Task<byte[]>> inProgress = new();

    public ImageCache(int capacity, int cleanThreshold = 4)
    {
        this.capacity = capacity;
        this.cleanThreshold = cleanThreshold;

        _ = CleanupLoopAsync(cts.Token);
    }

    public async Task<byte[]> GetOrAddAsync(
     string key,
     Func<byte[]> factory)
    {
        if (cache.TryGetValue(key, out var cached))
        {
            MoveToFront(key);

            Console.WriteLine($"[CACHE HIT] {key}");

            return cached;
        }

        object fileLock =
            fileLocks.GetOrAdd(key, _ => new object());

        Task<byte[]> task;
        bool createdTask = false;

        lock (fileLock)
        {
            if (cache.TryGetValue(key, out cached))
            {
                MoveToFront(key);
                return cached;
            }

            if (!inProgress.TryGetValue(key, out task))
            {
                task = Task.Run(factory);

                inProgress[key] = task;

                createdTask = true;
            }
        }

        try
        {
            byte[] data = await task;

            lock (fileLock)
            {
                if (!cache.ContainsKey(key))
                {
                    cache[key] = data;

                    lock (lruLock)
                    {
                        AddToFront(key);

                        while (cache.Count > capacity)
                        {
                            RemoveOldest();
                        }
                    }
                }
            }

            return data;
        }
        finally
        {
            if (createdTask)
            {
                inProgress.TryRemove(key, out _);
            }
        }
    }

    private void AddToFront(string key)
    {
        if (nodes.TryGetValue(key, out var existing))
        {
            lru.Remove(existing);
        }

        var node = lru.AddFirst(key);

        nodes[key] = node;
    }

    private void MoveToFront(string key)
    {
        lock (lruLock)
        {
            if (!nodes.TryGetValue(key, out var node))
                return;

            lru.Remove(node);

            nodes[key] = lru.AddFirst(key);
        }
    }
    private void RemoveOldest()
    {
        var last = lru.Last;

        if (last == null)
            return;

        string key = last.Value;

        Console.WriteLine($"[EVICT] {key}");

        cache.TryRemove(key, out _);

        nodes.Remove(key);

        lru.RemoveLast();
    }

    private async Task CleanupLoopAsync(
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(10),
                    token);

                lock (lruLock)
                {
                    if (lru.Count > cleanThreshold)
                    {
                        int target =
                            Math.Max(cleanThreshold / 2, 1);

                        while (lru.Count > target)
                        {
                            RemoveOldest();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[CLEANUP ERROR] {ex.Message}");
            }
        }
    }

    public void PrintCache()
    {
        lock (lruLock)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"CACHE {cache.Count}/{capacity}");

            foreach (var key in lru)
            {
                if (cache.TryGetValue(key, out var data))
                {
                    double mb =
                        data.Length /
                        1024.0 /
                        1024.0;

                    Console.WriteLine(
                        $" - {key} [{mb:F2} MB]");
                }
            }
        }
    }

    public void Shutdown()
    {
        cts.Cancel();
    }
}