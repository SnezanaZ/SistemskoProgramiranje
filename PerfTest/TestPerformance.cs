using System.Diagnostics;
using System.Net.Http;

public class PerformanceTest
{
    public static async Task Run()
    {
        int requests = 10_000;
        int parallel = 100;

        string url = "http://localhost:5050/test.jpg";
        HttpClient client = new HttpClient();

        var sw = Stopwatch.StartNew();
        var sem = new SemaphoreSlim(parallel);

        var tasks = new List<Task>();

        for (int i = 0; i < requests; i++)
        {
            await sem.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var res = await client.GetAsync(url);
                    await res.Content.ReadAsByteArrayAsync();
                }
                finally
                {
                    sem.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        sw.Stop();

        Console.WriteLine($"Time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Req/sec: {requests / (sw.ElapsedMilliseconds / 1000.0)}");
    }
}