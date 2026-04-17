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

    public byte[] GetOrAdd(string key, Func<byte[]> factory)
    {
        lock(lockObj)
        {
            if(cache.ContainsKey(key))
            {
                MoveToFront(key);
                return cache[key];
            }

            if(inProgress.Contains(key))
            {
                while(inProgress.Contains(key))
                    Monitor.Wait(lockObj);

                return cache[key];
            }

            inProgress.Add(key);
        }

        var data = factory();

        lock(lockObj)
        {
            if(cache.Count >= capacity)
            {
                var last = lru.Last.Value;

                lru.RemoveLast();
                cache.Remove(last);
                map.Remove(last);
            }

            cache[key] = data;

            var node = new LinkedListNode<string>(key);
            lru.AddFirst(node);
            map[key] = node;

            inProgress.Remove(key);
            Monitor.PulseAll(lockObj);
        }

        return data;
    }

    private void MoveToFront(string key)
    {
        var node = map[key];
        lru.Remove(node);
        lru.AddFirst(node);
        map[key] = node;
    }
}