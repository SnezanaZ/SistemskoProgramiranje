using System.Diagnostics;

public class HybridPerformanceTest
{
    public static async Task Run()
    {
        string baseUrl = "http://localhost:5050/";
        HttpClient client = new HttpClient();
        var sw = Stopwatch.StartNew();
        
        string stampedeFile = "sunflower.jpg"; 
        string[] cachedFiles = { "1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg" }; 
        
        int stampedeRequests = 500;
        int cacheHitRequests = 5000;
        var tasks = new List<Task>();

        Console.WriteLine(">>> INICIJALIZACIJA: Punim keš do kapaciteta (1-4.jpg)...");
        for(int i = 0; i < 4; i++) {
            await client.GetAsync(baseUrl + cachedFiles[i]);
        }

        Console.WriteLine(">>> START: Paralelni Stampede i Cache Hit zahtevi...");

        for (int i = 0; i < stampedeRequests; i++)
        {
            tasks.Add(Task.Run(async () => {
                var res = await client.GetAsync(baseUrl + stampedeFile);
                if (res.IsSuccessStatusCode) await res.Content.ReadAsByteArrayAsync();
            }));
        }


        for (int i = 0; i < cacheHitRequests; i++)
        {
            tasks.Add(Task.Run(async () => {
                var res = await client.GetAsync(baseUrl + cachedFiles[i % 5]);
                if (res.IsSuccessStatusCode) await res.Content.ReadAsByteArrayAsync();
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        Console.WriteLine("\n--- HIBRIDNI TEST ZAVRŠEN ---");
        Console.WriteLine($"Ukupno vreme: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Req/sec: {(stampedeRequests + cacheHitRequests) / (sw.ElapsedMilliseconds / 1000.0):N2}");
    }
}