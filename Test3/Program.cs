using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YelpClientPerf
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var locations = new List<string>
            {
                "New York",
                "San Francisco",
                "Los Angeles",
                "Chicago",
                "London",
                "Paris",
                "Berlin",
                "Tokyo",
                "Sydney",
                "Toronto"
            };

            using var client = new HttpClient();

            var tasks = new List<Task<long>>();
            var semaphore = new SemaphoreSlim(5); // max 5 paralelnih zahteva

            var swTotal = Stopwatch.StartNew();

            foreach (var loc in locations)
            {
                // više zahteva po lokaciji
                for (int i = 0; i < 5; i++)
                {
                    await semaphore.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            return await SendRequest(client, loc);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
            }

            var durations = await Task.WhenAll(tasks);

            swTotal.Stop();

            Console.WriteLine("=== Performance Test Finished ===");
            Console.WriteLine($"Total requests: {durations.Length}");
            Console.WriteLine($"Average time: {Math.Round(durations.Average(), 2)} ms");
            Console.WriteLine($"Fastest: {durations.Min()} ms | Slowest: {durations.Max()} ms");
            Console.WriteLine($"Total test time: {swTotal.ElapsedMilliseconds} ms");
            Console.WriteLine($"Throughput: {Math.Round(durations.Length / (swTotal.ElapsedMilliseconds / 1000.0), 2)} requests/sec");
        }

        private static async Task<long> SendRequest(HttpClient client, string location)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var url = $"http://localhost:8080/restaurants/?location={Uri.EscapeDataString(location)}";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                sw.Stop();

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Location: {location} | Status: {response.StatusCode} | Time: {sw.ElapsedMilliseconds} ms");

                // samo prvih 100 karaktera da ne zatrpa konzolu
                Console.WriteLine(content.Substring(0, Math.Min(content.Length, 100)));
                Console.WriteLine("---------------------------------------------------");

                return sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR for {location}: {ex.Message}");
                return sw.ElapsedMilliseconds;
            }
        }
    }
}
