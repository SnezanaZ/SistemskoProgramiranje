using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class ImageCache
{
    private readonly int capacity;
    private readonly int cleanThreshold;
    private readonly CancellationTokenSource cts = new();

    private readonly Task cleanupTask;
    private readonly ConcurrentDictionary<string, byte[]> cache = new();

    private readonly LinkedList<string> lru = new();
    private readonly Dictionary<string, LinkedListNode<string>> nodes = new();


    private readonly object lruLock = new();


    private readonly ConcurrentDictionary<string, Task<byte[]>> inProgress = new();

    public ImageCache(int capacity, int cleanThreshold = 4)
    {
        this.capacity = capacity;
        this.cleanThreshold = cleanThreshold;


        cleanupTask = CleanupLoopAsync(cts.Token);
    }

    public async Task<byte[]> GetOrAddAsync(string key, Func<Task<byte[]>> factory)
    {

        if (cache.TryGetValue(key, out var cached))
        {
            MoveToFront(key);
            Console.WriteLine($"[KEŠ POGODAK] {key}");
            return cached;
        }



        Task<byte[]> conversionTask = inProgress.GetOrAdd(key, _ => factory());

        try
        {
            byte[] data = await conversionTask;


            lock (lruLock)
            {
                if (cache.TryAdd(key, data))
                {
                    AddToFrontWithoutLock(key);
                    while (cache.Count > capacity)
                    {
                        RemoveOldestWithoutLock();
                    }
                }
            }

            return data;
        }
        finally
        {
            inProgress.TryRemove(key, out _);
        }
    }

    private void MoveToFront(string key)
    {
        lock (lruLock)
        {
            if (nodes.TryGetValue(key, out var node))
            {

                lru.Remove(node);
                lru.AddFirst(node);
            }
        }
    }

    private void AddToFrontWithoutLock(string key)
    {
        if (nodes.TryGetValue(key, out var existing))
        {
            lru.Remove(existing);
        }

        var node = lru.AddFirst(key);
        nodes[key] = node;
    }

    private void RemoveOldestWithoutLock()
    {
        var last = lru.Last;
        if (last == null) return;

        string key = last.Value;
        Console.WriteLine($"[IZBACIVANJE] Uklanjanje najstarijeg iz keša: {key}");

        cache.TryRemove(key, out _);
        nodes.Remove(key);
        lru.RemoveLast();
    }

    private async Task CleanupLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);

                lock (lruLock)
                {
                    if (lru.Count > cleanThreshold)
                    {
                        int target = Math.Max(cleanThreshold / 2, 1);

                        while (lru.Count > target)
                        {
                            RemoveOldestWithoutLock();
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
                Console.WriteLine($"[GREŠKA ČISTAČA] {ex.Message}");
            }
        }
    }

    public void PrintCache()
    {
        lock (lruLock)
        {
            Console.WriteLine($"\n>>> STANJE KEŠA: {cache.Count}/{capacity}");
            foreach (var key in lru)
            {
                if (cache.TryGetValue(key, out var data))
                {
                    double mb = data.Length / 1024.0 / 1024.0;
                    Console.WriteLine($" - {key} [{mb:F2} MB]");
                }
            }
        }
    }

    public async Task ShutdownAsync()
    {
        cts.Cancel();
        await cleanupTask;
    }
}