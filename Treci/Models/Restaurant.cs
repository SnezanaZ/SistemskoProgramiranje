using Newtonsoft.Json;

namespace Treci
{
    public class Restaurant
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("rating")]
        public double Rating { get; set; }

        [JsonProperty("review_count")]
        public int ReviewCount { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("is_closed")]
        public bool IsClosed { get; set; }

        public int PriceLevel =>
            Price?.Length ?? 0;
    }
}