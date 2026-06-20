using System.Collections.Generic;

namespace Treci
{
    public record FetchRequest(string Location);

    public record AggregatedData(
        List<Restaurant> Restaurants);

    public record SortedData(
        List<Restaurant> Restaurants);
}