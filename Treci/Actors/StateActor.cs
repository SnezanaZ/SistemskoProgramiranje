using System;
using System.Collections.Generic;
using System.Threading;
using Akka.Actor;

namespace Treci
{
    public class StateActor : ReceiveActor
    {
        private readonly Dictionary<string, List<Restaurant>> _cache = new();
        private readonly Dictionary<string, DateTime> _lastUpdated = new();
        private readonly Dictionary<IActorRef, string> _pendingSort = new();
        private readonly IActorRef _sortActor;
        public StateActor()
        {
             _sortActor = Context.ActorOf(
        Props.Create(() => new SortActor())
             .WithDispatcher("yelp-dispatcher"),
        "sorter");

            Receive<RestaurantBatch>(batch =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | Batch received | Location: {batch.Location} | Count: {batch.Restaurants.Count} | Thread: {Thread.CurrentThread.ManagedThreadId}");

                // var sortActor = Context.ActorOf(
                //     Props.Create(() => new SortActor())
                //          .WithDispatcher("yelp-dispatcher"),
                //     $"sort-{Guid.NewGuid():N}");

                // _pendingSort[sortActor] = batch.Location;
                // Context.Watch(sortActor);
                // sortActor.Tell(new AggregatedData(batch.Restaurants), Self);

                _pendingSort[_sortActor] = batch.Location;

_sortActor.Tell(
    new AggregatedData(batch.Restaurants),
    Self);
            });

            Receive<SortedData>(data =>
{
    /*if (!_pendingSort.Remove(Sender, out var location))
        return;

    if (!_cache.ContainsKey(location))
    {
        _cache[location] = new List<Restaurant>();
    }

    _cache[location] = _cache[location]
        .Concat(data.Restaurants)
        .GroupBy(r => r.Name)
        .Select(g => g.OrderByDescending(x => x.Rating).First())
        .OrderByDescending(r => r.PriceLevel)
        .ThenByDescending(r => r.Rating)
        .ToList();

    _lastUpdated[location] = DateTime.Now;*/
    if (!_pendingSort.Remove(Sender, out var location)) return;
    
    // Zameni staro stanje novim — ne Concat
    _cache[location] = data.Restaurants;
    _lastUpdated[location] = DateTime.Now;

    Console.WriteLine(
        $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | Cache updated | " +
        $"Location: {location} | Count: {_cache[location].Count}");
});

            // Terminated: ako SortActor nije u _pendingSort — već je završio normalno, ignoriši.
            // Ako jeste — crashovao je pre odgovora, čisti unos.
            Receive<Terminated>(t =>
            {
                if (_pendingSort.Remove(t.ActorRef, out var location))
                {
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | SortActor crashed | Location: {location} — cleaned up");
                }
                // else: normalan završetak — ništa
            });

            Receive<GetCachedData>(req =>
            {
                var isReady = _cache.ContainsKey(req.Location);
                var restaurants = isReady
                    ? new List<Restaurant>(_cache[req.Location])
                    : new List<Restaurant>();

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | GetCachedData | Location: {req.Location} | Ready: {isReady} | Count: {restaurants.Count}" +
                    (isReady ? $" | Updated: {_lastUpdated[req.Location]:HH:mm:ss}" : " | Not yet cached"));

                Sender.Tell(new CachedDataResponse(req.Location, restaurants, isReady));
            });
        }

        protected override void PostStop()
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | Stopped | Cached: {string.Join(", ", _cache.Keys)}");
            base.PostStop();
        }
    }
}