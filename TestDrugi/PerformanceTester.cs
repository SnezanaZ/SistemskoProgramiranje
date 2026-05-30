using System.Diagnostics;
using System.Net.Http;

public class PerformanceTester
{
    private readonly HttpClient client = new();

    public async Task RunAllTests()
    {
        Console.WriteLine("===== TEST 1: Cache Hit/Miss =====");
        await CacheHitMissTest();

        Console.WriteLine("\n===== TEST 2: Cache Stampede =====");
        await CacheStampedeTest();

        Console.WriteLine("\n===== TEST 3: Različite slike =====");
        await DifferentImagesTest();

        Console.WriteLine("\n===== TEST 4: Stress Test =====");
        await StressTest();
    }

    private async Task CacheHitMissTest()
    {
        Stopwatch sw = Stopwatch.StartNew();

        await client.GetAsync(
            "http://localhost:5050/1.jpg");

        sw.Stop();

        Console.WriteLine(
            $"Prvi zahtev: {sw.ElapsedMilliseconds} ms");

        sw.Restart();

        await client.GetAsync(
            "http://localhost:5050/1.jpg");

        sw.Stop();

        Console.WriteLine(
            $"Drugi zahtev (cache hit): {sw.ElapsedMilliseconds} ms");
    }

    private async Task CacheStampedeTest()
    {
        Stopwatch sw = Stopwatch.StartNew();

        List<Task<HttpResponseMessage>> tasks = new();

        for (int i = 0; i < 20; i++)
        {
            tasks.Add(
                client.GetAsync(
                    "http://localhost:5050/2.jpg"));
        }

        await Task.WhenAll(tasks);

        sw.Stop();

        Console.WriteLine(
            $"20 paralelnih zahteva za ISTU sliku: {sw.ElapsedMilliseconds} ms");

        Console.WriteLine(
            "Na serveru treba da se vidi samo JEDNA konverzija.");
    }

    private async Task DifferentImagesTest()
    {
        Stopwatch sw = Stopwatch.StartNew();

        List<Task<HttpResponseMessage>> tasks = new();

        for (int i = 1; i <= 5; i++)
        {
            tasks.Add(
                client.GetAsync(
                    $"http://localhost:5050/{i}.jpg"));
        }

        await Task.WhenAll(tasks);

        sw.Stop();

        Console.WriteLine(
            $"10 različitih slika: {sw.ElapsedMilliseconds} ms");
    }

    private async Task StressTest()
    {
        Stopwatch sw = Stopwatch.StartNew();

        List<Task<HttpResponseMessage>> tasks = new();

        for (int i = 0; i < 100; i++)
        {
            int img = (i % 5) + 1;

            tasks.Add(
                client.GetAsync(
                    $"http://localhost:5050/{img}.jpg"));
        }

        await Task.WhenAll(tasks);

        sw.Stop();

        Console.WriteLine(
            $"100 zahteva završeno za {sw.ElapsedMilliseconds} ms");
    }
}