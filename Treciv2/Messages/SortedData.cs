namespace Treciv2
{
    public sealed class SortedData
    {
        public SortedData(List<Restaurant> restaurants)
        {
            Restaurants = restaurants;
        }

        public List<Restaurant> Restaurants { get; }
    }
}