using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Akka.Actor;

namespace Treci
{
    public class FetchActor : ReceiveActor
    {
        private readonly YelpRxService _service = new();

        // ConcurrentBag - thread-safe kolekcija jer OnNext dolazi sa TaskPoolScheduler threada
        private readonly ConcurrentBag<Restaurant> _restaurants = new();

        private IDisposable _subscription;

        public FetchActor()
        {
            Receive<FetchRequest>(req =>
            {
                var parent = Sender;

                // Čisti prethodno stanje
                while (!_restaurants.IsEmpty)
                    _restaurants.TryTake(out _);

                _subscription?.Dispose();

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] FETCH ACTOR | Received FetchRequest | Location: {req.Location} | Thread: {Thread.CurrentThread.ManagedThreadId}");

                _subscription = _service
                    .GetRestaurants(req.Location)
                    // Rx filtriranje — samo kvalitetni, otvoreni restorani
                    .Where(r => r.Rating > 4.0 && r.ReviewCount > 500 && !r.IsClosed)
                    // ObserveOn — OnNext/OnError/OnCompleted callbacks na thread pool
                    .ObserveOn(TaskPoolScheduler.Default)
                    .Subscribe(
                        onNext: r =>
                        {
                            _restaurants.Add(r);
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss}] FETCH ACTOR | Collected: {r.Name} " +
                                $"(Rating: {r.Rating}, Reviews: {r.ReviewCount}) | Thread: {Thread.CurrentThread.ManagedThreadId}");
                        },
                        onError: ex =>
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss}] FETCH ACTOR | ERROR: {ex.Message}");
                            parent.Tell(new Status.Failure(ex));
                        },
                        onCompleted: () =>
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss}] FETCH ACTOR | Stream completed | " +
                                $"Total collected: {_restaurants.Count} | Thread: {Thread.CurrentThread.ManagedThreadId}");
                            parent.Tell(new AggregatedData(new List<Restaurant>(_restaurants)));
                        });
            });
        }

        protected override void PostStop()
        {
            _subscription?.Dispose();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] FETCH ACTOR | Stopped");
            base.PostStop();
        }
    }
}