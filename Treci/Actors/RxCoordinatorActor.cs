using System;
using System.Collections.Generic;
using System.Threading;
using Akka.Actor;

namespace Treci
{
    public class RxCoordinatorActor : ReceiveActor
    {
        private readonly YelpRxService _service = new();
        private readonly IActorRef _stateActor;
        private readonly TimeSpan _pollInterval;

        private readonly Dictionary<string, IDisposable> _subscriptions = new();

        public RxCoordinatorActor(IActorRef stateActor, TimeSpan pollInterval)
        {
            _stateActor = stateActor;
            _pollInterval = pollInterval;

            Receive<StartPolling>(msg =>
            {
                var location = msg.Location;

                if (_subscriptions.ContainsKey(location))
                {
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}] RX COORDINATOR | Polling already active for: {location} — skip");
                    return;
                }

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] RX COORDINATOR | Starting periodic poll | " +
                    $"Location: {location} | Interval: {_pollInterval.TotalSeconds}s | " +
                    $"Thread: {Thread.CurrentThread.ManagedThreadId}");

                var subscription = _service
                    .PollRestaurantsPeriodically(location, _pollInterval, new FilterCriteria(4.0, 500, true))
                    .Subscribe(
                        onNext: batch =>
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss}] RX COORDINATOR | Emitting batch → StateActor | " +
                                $"Location: {batch.Location} | Count: {batch.Restaurants.Count}");

                        
                            _stateActor.Tell(batch);
                        },
                       onError: ex =>
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss}] RX COORDINATOR | Stream error for {location}: {ex.Message}");
                            _subscriptions.Remove(location);
                        });

                _subscriptions[location] = subscription;
            });

            Receive<StopPolling>(msg =>
{
    if (_subscriptions.TryGetValue(msg.Location, out var sub))
    {
        sub.Dispose();
        _subscriptions.Remove(msg.Location);

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] RX COORDINATOR | Stopped polling {msg.Location}");
    }
});
        }

        protected override void PostStop()
        {
            foreach (var (location, sub) in _subscriptions)
            {
                sub.Dispose();
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] RX COORDINATOR | Disposed subscription for: {location}");
            }
            base.PostStop();
        }
    }
}