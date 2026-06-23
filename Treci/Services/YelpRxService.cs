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

        public IObservable<Restaurant> GetRestaurants(string location)
        {
            return Observable
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

                    // return root["businesses"]
                    //     ?.ToObject<List<Restaurant>>()
                    //     ?? new List<Restaurant>();

                    var arr = root["businesses"] as JArray;

        return arr?
            .Select(b => new Restaurant
            {
                Name = (string)b["name"],
                Rating = (double?)b["rating"] ?? 0,
                ReviewCount = (int?)b["review_count"] ?? 0,
                Price = (string)b["price"],
                IsClosed = (bool?)b["is_closed"] ?? false
            })
            .ToList()
            ?? new List<Restaurant>();
                })
                .SubscribeOn(TaskPoolScheduler.Default)   // API poziv na thread pool
                .Timeout(TimeSpan.FromSeconds(10))
                .Retry(2)
                .SelectMany(x => x);
        }
    }
}