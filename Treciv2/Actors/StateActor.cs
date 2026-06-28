using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Treciv2
{
    public class StateActor : ReceiveActor
    {
        private readonly Dictionary<string, List<Restaurant>> _cache = new();
        private readonly Dictionary<IActorRef, string> _pending = new();

        public StateActor()
        {
            Receive<RestaurantBatch>(batch =>
            {
                var sort = Context.ActorOf(
                    Props.Create(() => new SortActor()),
                    $"sort-{Guid.NewGuid():N}");

                _pending[sort] = batch.Location;

                Context.Watch(sort);

                sort.Tell(new AggregatedData(batch.Restaurants), Self);
            });

            Receive<SortedData>(data =>
            {
                if (!_pending.Remove(Sender, out var location))
                    return;

                // IMMUTABLE UPDATE STYLE
                _cache[location] = new List<Restaurant>(data.Restaurants);

                Console.WriteLine($"STATE UPDATED | {location} | {_cache[location].Count}");
            });

            Receive<Terminated>(t =>
            {
                _pending.Remove(t.ActorRef);
            });

            Receive<GetCachedData>(req =>
{
    var exists = _cache.TryGetValue(req.Location, out var data);

    Sender.Tell(new CachedDataResponse(
        req.Location,
        exists ? new List<Restaurant>(data) : new List<Restaurant>(),
        exists
    ));
});
        }
    }
}