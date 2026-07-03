using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;

namespace Treci
{
    public class LocationActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly string _location;
        private readonly YelpRxService _service = new();

        private List<Restaurant> _restaurants = new();
        private DateTime? _lastUpdated;
        private readonly HashSet<IActorRef> _pendingSorts = new();
        private readonly List<IActorRef> _waiting = new();
        private IDisposable _rxSubscription;

        public LocationActor(string location)
        {
            _location = location;
            var self = Self;

            Receive<StartPeriodicFetch>(start =>
            {
                if (_rxSubscription != null) return;

                _log.Info($"[LOCATION ACTOR:{_location}] Rx pokrenut | Interval: {start.Interval.TotalSeconds}s");

                _rxSubscription = _service
                    .PollRestaurantsPeriodically(_location, start.Interval, new FilterCriteria(4.0, 500, true))
                    .Subscribe(
                        onNext: batch => self.Tell(batch),
                        onError: ex => _log.Error(ex, $"[LOCATION ACTOR:{_location}] Rx stream greška"));
            });

            Receive<RestaurantBatch>(batch =>
            {
                _log.Info($"[LOCATION ACTOR:{_location}] Batch primljen | Count: {batch.Restaurants.Count}");

                var sortActor = Context.ActorOf(
                    Props.Create(() => new SortActor())
                         .WithDispatcher("yelp-dispatcher"),
                    $"sort-{Guid.NewGuid():N}");

                _pendingSorts.Add(sortActor);
                Context.Watch(sortActor);
                sortActor.Tell(new AggregatedData(batch.Restaurants), Self);
            });

            Receive<SortedData>(data =>
            {
                if (!_pendingSorts.Remove(Sender)) return;

                _restaurants = _restaurants
                    .Concat(data.Restaurants)
                    .GroupBy(r => r.Name)
                    .Select(g => g.OrderByDescending(x => x.Rating).First())
                    .OrderByDescending(r => r.PriceLevel)
                    .ThenByDescending(r => r.Rating)
                    .ToList();

                _lastUpdated = DateTime.Now;

                if (_waiting.Count > 0)
                {
                    _log.Info($"[LOCATION ACTOR:{_location}] Obaveštavam {_waiting.Count} čekajućih pošiljalaca");

                    foreach (var waiter in _waiting)
                        waiter.Tell(new CachedDataResponse(_location, new List<Restaurant>(_restaurants), true));

                    _waiting.Clear();
                }
            });

            Receive<Terminated>(t =>
            {
                if (_pendingSorts.Remove(t.ActorRef))
                    _log.Warning($"[LOCATION ACTOR:{_location}] SortActor je pao — očišćeno");
            });

            Receive<GetCachedData>(req =>
            {
                if (_lastUpdated.HasValue)
                {
                    Sender.Tell(new CachedDataResponse(_location, new List<Restaurant>(_restaurants), true));
                }
                else
                {
                    _waiting.Add(Sender);
                    _log.Info($"[LOCATION ACTOR:{_location}] Pošiljalac u redu čekanja | Waiting: {_waiting.Count}");
                }
            });

            Receive<StopPolling>(_ =>
            {
                _rxSubscription?.Dispose();
                _rxSubscription = null;
                _log.Info($"[LOCATION ACTOR:{_location}] Polling zaustavljen");
            });
        }

        protected override void PostStop()
        {
            _rxSubscription?.Dispose();
            _log.Info($"[LOCATION ACTOR:{_location}] Zaustavljen | Count: {_restaurants.Count}");
            base.PostStop();
        }
    }
}