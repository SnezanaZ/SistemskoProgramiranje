using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

public class ImageCache
{
    private readonly int capacity;
// Prag nakon kojeg pozadinska nit kreće u čišćenje (npr. kada pređe 4 elementa)
    private readonly int cleanThreshold;
    // Token za bezbedno gašenje pozadinske niti kada se server gasi
  
    private readonly CancellationTokenSource cts = new();
    private Dictionary<string, LinkedListNode<string>> map = new();
    private Dictionary<string, byte[]> cache = new();
    private LinkedList<string> lru = new();

    private HashSet<string> inProgress = new();
    private object lockObj = new();

    public ImageCache(int capacity,int cleanThreshold=4)
    {
        this.capacity = capacity;
        this.cleanThreshold=cleanThreshold;

        // POKRETANJE POZADINSKOG ČISTAČA KROZ TASK
        // TaskCreationOptions.LongRunning govori sistemu da ova nit živi dugo i da ne opterećuje ThreadPool
        Task.Factory.StartNew(async () => await PeriodicCleanupAsync(cts.Token), 
            cts.Token, 
            TaskCreationOptions.LongRunning, 
            TaskScheduler.Default);
    }
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
               /* if (cache.Count >= capacity)
                {
                    var last = lru.Last;
                    if (last != null)
                    {
                        cache.Remove(last.Value);
                        map.Remove(last.Value);
                        lru.RemoveLast();
                    }
                }*/
                // Reaktivno čišćenje ako se dosegne apsolutni maksimum kapaciteta
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
    //DODATO
    private async Task PeriodicCleanupAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Nit spava npr. 10 sekundi pre sledeće provere (provera se prekida odmah ako se server gasi)
                await Task.Delay(TimeSpan.FromSeconds(10), token);

                lock (lockObj)
                {
                    // Ako niko ne zove keš, a nakupilo se više elemenata od praga (cleanThreshold)
                    if (lru.Count > cleanThreshold)
                    {
                        Console.WriteLine($"\n[POZADINSKI ČISTAČ] Detektovano {lru.Count} elemenata (Prag je {cleanThreshold}). Čišćenje u toku...");
                        
                        // Čistimo dok ne spustimo broj elemenata na bezbednu polovinu kapaciteta praga
                        int targetCount = cleanThreshold / 2; 
                        while (lru.Count > targetCount)
                        {
                            EvictOldestWithoutLock();
                        }
                        
                        Console.WriteLine($"[POZADINSKI ČISTAČ] Keš očišćen. Trenutno stanje: {lru.Count} elemenata.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normalno ponašanje prilikom gašenja aplikacije preko CancellationToken-a
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GREŠKA ČISTAČA] {ex.Message}");
            }
        }
    }

    // Pomoćna metoda za izbacivanje najstarijeg elementa (mora se zvati unutar lock-a)
    private void EvictOldestWithoutLock()
    {
        var last = lru.Last;
        if (last != null)
        {
            Console.WriteLine($"[IZBACIVANJE] POZADINSKI/REAKTIVNI ČISTAČ uklanja: {last.Value}");
            cache.Remove(last.Value);
            map.Remove(last.Value);
            lru.RemoveLast();
        }
    }
    // Metoda za bezbedno stopiranje pozadinske niti prilikom gašenja servera
    public void Shutdown()
    {
        cts.Cancel();
    }
}