using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Treci
{
    public class YelpRxService
    {
        private static readonly HttpClient _client = new HttpClient();

        static YelpRxService()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            var apiKey = configuration["YelpApiKey"];
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <summary>
        /// Periodično (svaki interval) poziva Yelp API za datu lokaciju,
        /// filtrira i mapira rezultate, i emituje ih kao RestaurantBatch poruke.
        /// Ovo se pokreće nezavisno od web zahteva.
        /// 
        /// ISPRAVKA: Umesto rekurzivnog Catch (koji pravi stack overflow pri
        /// svakoj grešci), koristimo Observable.Defer + .Retry() koji interno
        /// resubscribuje na isti observable bez gomilanja framera na steku.
        /// </summary>
        public IObservable<RestaurantBatch> PollRestaurantsPeriodically(
            string location,
            TimeSpan interval)
        {
            return Observable.Defer(() => BuildPollStream(location, interval))
                .Retry(); // Beskonačan retry — svaka greška pokreće novi Defer (novi stream)
        }


        private IObservable<RestaurantBatch> BuildPollStream(string location, TimeSpan interval)
        {
            return Observable
                // Okida odmah (0), pa zatim svakih `interval`
                .Timer(TimeSpan.Zero, interval, TaskPoolScheduler.Default)
                .Do(tick => Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] RX POLL | Tick #{tick} | Location: {location} | Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}"))
                // Za svaki tick, asinhrono pozovi API
                .SelectMany(tick => Observable
    .FromAsync(async () =>
    {
        var url =
            $"https://api.yelp.com/v3/businesses/search" +
            $"?term=restaurants" +
            $"&location={Uri.EscapeDataString(location)}" +
            $"&limit=50";

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] YELP API CALL | Location: {location} | Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

        var json = await _client.GetStringAsync(url);
        var root = JObject.Parse(json);
        var arr = root["businesses"] as JArray;

        return arr?
            .Select(b => new Restaurant
            {
                Name = (string)b["name"],
                Rating = (double?)b["rating"] ?? 0,
                ReviewCount = (int?)b["review_count"] ?? 0,
                Price = (string?)b["price"] ?? "",
                IsClosed = (bool?)b["is_closed"] ?? false
            })
            .ToList()
            ?? new List<Restaurant>();
    })
    .SubscribeOn(TaskPoolScheduler.Default)
    .Timeout(TimeSpan.FromSeconds(10))
    .Retry(2)
    .Catch<List<Restaurant>, Exception>(ex =>
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] YELP API ERROR | Location: {location} | {ex.Message} — skipping tick");
        return Observable.Return(new List<Restaurant>());
    })
)
                // Rx filtriranje i mapiranje — samo kvalitetni, otvoreni restorani
                .Select(list =>
                {
                    var filtered = list
                        .Where(r => r.Rating > 4.0 && r.ReviewCount > 500 && !r.IsClosed)
                        .ToList();

                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}] RX FILTER | Location: {location} | " +
                        $"Raw: {list.Count} → Filtered: {filtered.Count}");

                    return new RestaurantBatch(location, filtered);
                }).ObserveOn(TaskPoolScheduler.Default);
        }
    }
}
