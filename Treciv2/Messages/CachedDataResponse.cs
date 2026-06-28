namespace Treciv2
{
    public sealed class CachedDataResponse
    {
        public CachedDataResponse(string location,
            List<Restaurant> restaurants,
            bool isReady)
        {
            Location = location;
            Restaurants = restaurants;
            IsReady = isReady;
        }

        public string Location { get; }

        public List<Restaurant> Restaurants { get; }

        public bool IsReady { get; }
    }
}