using System.Collections.Generic;
using System.Threading;

public class ImageCache
{
    private readonly int capacity;

    private Dictionary<string, LinkedListNode<string>> map = new();
    private Dictionary<string, byte[]> cache = new();
    private LinkedList<string> lru = new();

    private HashSet<string> inProgress = new();
    private object lockObj = new();

    public ImageCache(int capacity)
    {
        this.capacity = capacity;
    }

    //    public byte[] GetOrAdd(string key, Func<byte[]> factory)
    //     {
    //         lock (lockObj)
    //         {
    //             if (cache.ContainsKey(key))
    //             {
    //                 Console.WriteLine($"[KEŠ POGODAK] {key}");
    //                 MoveToFront(key);
    //                 return cache[key];
    //             }

    //             Console.WriteLine($"[KEŠ PROMAŠAJ] {key}");

    //             while (inProgress.Contains(key))
    //             {
    //                 Monitor.Wait(lockObj);
    //             }

    //             if (cache.ContainsKey(key))
    //             {
    //                 return cache[key];
    //             }

    //             inProgress.Add(key);
    //         }

    //         var data = factory();

    //         lock (lockObj)
    //         {
    //             if (cache.Count >= capacity && !cache.ContainsKey(key))
    //             {
    //                 var last = lru.Last.Value;
    //                 lru.RemoveLast();
    //                 cache.Remove(last);
    //                 map.Remove(last);
    //             }

    //             cache[key] = data;
    //             var node = new LinkedListNode<string>(key);
    //             lru.AddFirst(node);
    //             map[key] = node;

    //             inProgress.Remove(key);
    //             Monitor.PulseAll(lockObj);
    //         }

    //         return data;
    //     }
    //     private void MoveToFront(string key)
    //     {
    //         var node = map[key];
    //         lru.Remove(node);
    //         lru.AddFirst(node);
    //         map[key] = node;
    //     }
    public byte[] GetOrAdd(string key, Func<byte[]> factory)
    {

        lock (lockObj)
        {
            if (cache.TryGetValue(key, out var cachedData))
            {
                Console.WriteLine($"[KEŠ POGODAK] {key}");
                MoveToFront(key);
                return cachedData;
            }


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


            Console.WriteLine($"[KEŠ PROMAŠAJ] {key}");
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
                    var last = lru.Last;
                    if (last != null)
                    {
                        cache.Remove(last.Value);
                        map.Remove(last.Value);
                        lru.RemoveLast();
                    }
                }
                cache[key] = newData;
                map[key] = lru.AddFirst(key);
            }
        }

        return newData;
    }

    private void MoveToFront(string key)
    {

        if (map.TryGetValue(key, out var node))
        {
            lru.Remove(node);
            lru.AddFirst(node);
        }
    }

    public void PrintCache()
    {
        lock (lockObj)
        {
            Console.WriteLine($"\n>>> STANJE KEŠA: {lru.Count}/{capacity}");

            if (lru.Count == 0)
            {
                Console.WriteLine("Keš je prazan.");
            }
            else
            {
                foreach (var fajl in lru)
                {
                    double mb = cache[fajl].Length / 1024.0 / 1024.0;
                    Console.WriteLine($" - {fajl} [{mb:F2} MB]");
                }
            }
            Console.WriteLine("--------------------------\n");
        }
    }
}