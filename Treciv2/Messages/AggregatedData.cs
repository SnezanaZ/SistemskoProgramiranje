namespace Treciv2
{
    public sealed class AggregatedData
    {
        public AggregatedData(List<Restaurant> restaurants)
        {
            Restaurants = restaurants;
        }

        public List<Restaurant> Restaurants { get; }
    }
}