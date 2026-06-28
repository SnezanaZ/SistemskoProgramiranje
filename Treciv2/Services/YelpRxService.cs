using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Akka.Actor;
namespace Treciv2
{
    public class YelpRxService
    {
        private static readonly HttpClient _client = new HttpClient();

        static YelpRxService()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var apiKey = config["YelpApiKey"];

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public IObservable<RestaurantBatch> PollRestaurantsPeriodically(
            string location,
            TimeSpan interval)
        {
            return Observable
                .Timer(TimeSpan.Zero, interval, TaskPoolScheduler.Default)
                .SelectMany(_ => Fetch(location))
                .Where(list => list.Count > 0)
                .Select(list =>
                    new RestaurantBatch(location, list));
        }

        private IObservable<List<Restaurant>> Fetch(string location)
        {
            return Observable.FromAsync(async () =>
            {
                var url =
                    "https://api.yelp.com/v3/businesses/search" +
                    $"?term=restaurants&location={Uri.EscapeDataString(location)}&limit=50";

                var json = await _client.GetStringAsync(url);

                var arr = JObject.Parse(json)["businesses"];

                var list = arr?
                    .Select(b => new Restaurant
                    {
                        Name = (string)b["name"],
                        Rating = (double?)b["rating"] ?? 0,
                        ReviewCount = (int?)b["review_count"] ?? 0,
                        Price = (string?)b["price"] ?? "",
                        IsClosed = (bool?)b["is_closed"] ?? false
                    })
                    .Where(r =>
                        r.Rating > 4.0 &&
                        r.ReviewCount > 500 &&
                        !r.IsClosed)
                    .ToList()
                    ?? new List<Restaurant>();

                return list;
            })
            .SubscribeOn(TaskPoolScheduler.Default);
        }
    }
}