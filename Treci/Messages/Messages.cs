using System;
using System.Collections.Generic;

namespace Treci
{
    public record FetchRequest(string Location);
    public record RestaurantBatch(string Location, List<Restaurant> Restaurants);
    public record AggregatedData(List<Restaurant> Restaurants);
    public record SortedData(List<Restaurant> Restaurants);
    public record GetCachedData(string Location);
    public record CachedDataResponse(string Location, List<Restaurant> Restaurants, bool IsReady);
    public record StartPeriodicFetch(string Location, TimeSpan Interval);
    public record StopPolling(string Location);
    public record FilterCriteria(double MinRating, int MinReviews, bool OnlyOpen);
}