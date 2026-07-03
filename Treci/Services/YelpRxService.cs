using System;
using System.Collections.Generic;
using System.Linq;
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

        public IObservable<RestaurantBatch> PollRestaurantsPeriodically(
            string location,
            TimeSpan interval,
            FilterCriteria criteria = null)
        {
            return Observable.Defer(() => BuildPollStream(location, interval, criteria))
                .Retry();
        }

        private IObservable<RestaurantBatch> BuildPollStream(string location, TimeSpan interval, FilterCriteria criteria)
        {
            return Observable
                .Timer(TimeSpan.Zero, interval, TaskPoolScheduler.Default)
                .Do(tick => Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] RX POLL | Tick #{tick} | Location: {location} | Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}"))
                .SelectMany(tick => Observable
                    .FromAsync(async () =>
                    {
                        var url =
                            $"https://api.yelp.com/v3/businesses/search" +
                            $"?term=restaurants" +
                            $"&location={Uri.EscapeDataString(location)}" +
                            $"&open_now=true" +
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
                .Select(list =>
                {
                    var filtered = list
                        .Where(r => r.Rating > criteria.MinRating &&
                                    r.ReviewCount > criteria.MinReviews &&
                                    (!criteria.OnlyOpen || !r.IsClosed))
                        .ToList();

                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}] RX FILTER | Location: {location} | " +
                        $"Raw: {list.Count} → Filtered: {filtered.Count}");

                    return new RestaurantBatch(location, filtered);
                }).ObserveOn(TaskPoolScheduler.Default);
        }
    }
}