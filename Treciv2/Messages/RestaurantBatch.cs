namespace Treciv2
{
    public sealed class RestaurantBatch
    {
        public RestaurantBatch(string location,
            List<Restaurant> restaurants)
        {
            Location = location;
            Restaurants = restaurants;
        }

        public string Location { get; }

        public List<Restaurant> Restaurants { get; }
    }
}