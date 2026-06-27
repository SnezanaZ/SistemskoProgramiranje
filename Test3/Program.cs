using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TreciClient
{
    class Program
    {
        private static readonly HttpClient _client = new();
        private const string BaseUrl = "http://localhost:8080/restaurants/";

        private static readonly List<string> Locations = new()
        {
            "Belgrade", "London", "New York", "Paris", "Tokyo"
        };

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Yelp Restaurant Client ===");
            Console.WriteLine($"Lokacije: {string.Join(", ", Locations)}");
            Console.WriteLine(new string('=', 40));
            Console.WriteLine();

            foreach (var location in Locations)
            {
                await FetchAndDisplay(location);
                Console.WriteLine();
            }

            Console.WriteLine("=== Gotovo ===");
        }

        private static async Task FetchAndDisplay(string location)
        {
            var url = $"{BaseUrl}?location={Uri.EscapeDataString(location)}";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Zahtev za: {location}");

            const int maxRetries = 10;
            const int retryDelayMs = 3000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _client.GetAsync(url);
                    var body = await response.Content.ReadAsStringAsync();

                    if ((int)response.StatusCode == 503)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Podaci se ucitavaju...");
                        await Task.Delay(retryDelayMs);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var err = JObject.Parse(body);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Greska: {err["message"] ?? err["error"]}");
                        return;
                    }

                    var json = JObject.Parse(body);
                    var restaurants = json["restaurants"] as JArray;
                    var count = (int)json["count"];

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Rezultati za: {location} ({count} restorana)");
                    Console.WriteLine(new string('-', 60));

                    if (count == 0)
                    {
                        Console.WriteLine("  Nema restorana koji zadovoljavaju kriterijume.");
                        return;
                    }

                    int rank = 1;
                    foreach (var r in restaurants)
                    {
                        var name    = (string)r["name"];
                        var rating  = (double)r["rating"];
                        var reviews = (int)r["review_count"];
                        var price   = (string)r["price"] ?? "N/A";

                        Console.WriteLine($"  {rank,2}. {name}");
                        Console.WriteLine($"      Ocena: {rating:F1} | Recenzije: {reviews} | Cena: {price}");
                        rank++;
                    }

                    Console.WriteLine(new string('-', 60));
                    return;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Greska pri povezivanju: {ex.Message}");
                    Console.WriteLine("Da li server radi na http://localhost:8080?");
                    return;
                }
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Podaci nisu stigli nakon {maxRetries} pokusaja.");
        }
    }
}