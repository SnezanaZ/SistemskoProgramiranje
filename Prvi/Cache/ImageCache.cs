using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;

public class ImageCache
{
    private readonly int capacity;
    private readonly int cleanThreshold;
    private readonly CancellationTokenSource cts = new();

    private ConcurrentDictionary<string, LinkedListNode<string>> map = new();
    private ConcurrentDictionary<string, byte[]> cache = new();
    
    private LinkedList<string> lru = new();
    private HashSet<string> inProgress = new();
    private object lockObj = new();

    public ImageCache(int capacity, int cleanThreshold = 4)
    {
        this.capacity = capacity;
        this.cleanThreshold = cleanThreshold;

        Task.Factory.StartNew(async () => await PeriodicCleanupAsync(cts.Token),
            cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public byte[] GetOrAdd(string key, Func<byte[]> factory)
    {
        if (cache.TryGetValue(key, out var cachedData))
        {
            Console.WriteLine($"[KEŠ POGODAK] {key}");
            MoveToFront(key);
            return cachedData;
        }

        lock (lockObj)
        {
            while (inProgress.Contains(key))
            {
                Console.WriteLine($"[ČEKANJE] Nit čeka na generisanje fajla: {key}");
                Monitor.Wait(lockObj);

                if (cache.TryGetValue(key, out var data))
                {
                    MoveToFront(key);
                    return data;
                }
            }
            inProgress.Add(key);
        }

        byte[] newData;
        try
        {
            newData = factory();
        }
        finally
        {
            lock (lockObj)
            {
                inProgress.Remove(key);
                Monitor.PulseAll(lockObj);
            }
        }

        lock (lockObj)
        {
            if (!cache.ContainsKey(key))
            {
                if (cache.Count >= capacity)
                {
                    EvictOldestWithoutLock();
                }
                cache[key] = newData;
                map[key] = lru.AddFirst(key);
            }
        }

        return newData;
    }

    private void MoveToFront(string key)
    {
        lock (lockObj)
        {
            if (map.TryGetValue(key, out var node))
            {
                lru.Remove(node);
                lru.AddFirst(node);
            }
        }
    }

    private async Task PeriodicCleanupAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);

                lock (lockObj)
                {
                    if (lru.Count > cleanThreshold)
                    {
                        int targetCount = cleanThreshold / 2;
                        while (lru.Count > targetCount)
                        {
                            EvictOldestWithoutLock();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Console.WriteLine($"[GREŠKA ČISTAČA] {ex.Message}"); }
        }
    }

    private void EvictOldestWithoutLock()
    {
        var last = lru.Last;
        if (last != null)
        {
            Console.WriteLine($"[IZBACIVANJE] Uklanjanje: {last.Value}");
            cache.TryRemove(last.Value, out _);
            map.TryRemove(last.Value, out _);
            lru.RemoveLast();
        }
    }

    public void PrintCache()
    {
        lock (lockObj)
        {
            Console.WriteLine($"\n>>> STANJE KEŠA: {cache.Count}/{capacity}");
            foreach (var fajl in lru)
            {
                double mb = cache[fajl].Length / 1024.0 / 1024.0;
                Console.WriteLine($" - {fajl} [{mb:F2} MB]");
            }
        }
    }

    public void Shutdown() => cts.Cancel();
}