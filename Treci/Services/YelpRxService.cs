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
        private static readonly HttpClient _client =
            new HttpClient();

        static YelpRxService()
        {
            var configuration =
                new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var apiKey = configuration["YelpApiKey"];

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);
        }

        public IObservable<Restaurant>
            GetRestaurants(string location)
        {
            return Observable
                .FromAsync(async () =>
                {
                    var url =
                        $"https://api.yelp.com/v3/businesses/search?location={Uri.EscapeDataString(location)}";

                    var json =
                        await _client.GetStringAsync(url);

                    var root =
                        JObject.Parse(json);

                    return root["businesses"]
                        ?.ToObject<List<Restaurant>>()
                        ?? new List<Restaurant>();
                })
                .Timeout(TimeSpan.FromSeconds(10))
                .Retry(2)
                .SelectMany(x => x)
                .SubscribeOn(TaskPoolScheduler.Default);
        }
    }
}