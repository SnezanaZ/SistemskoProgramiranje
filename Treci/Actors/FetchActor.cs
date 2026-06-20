using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Akka.Actor;

namespace Treci
{
    public class FetchActor : ReceiveActor
    {
        private readonly YelpRxService _service =
            new();

        private readonly List<Restaurant>
            _restaurants = new();

        private IDisposable _subscription;

        public FetchActor()
        {
            Receive<FetchRequest>(req =>
            {
                var parent = Sender;

                _restaurants.Clear();

                _subscription?.Dispose();

                _subscription =
                    _service
                        .GetRestaurants(req.Location)

                        .Where(r =>
                            r.Rating > 4.0 &&
                            r.ReviewCount > 500 &&
                            !r.IsClosed)

                        .ObserveOn(
                            TaskPoolScheduler.Default)

                        .Subscribe(
                            r =>
                            {
                                _restaurants.Add(r);
                            },

                            ex =>
                            {
                                parent.Tell(
                                    new Status.Failure(ex));
                            },

                            () =>
                            {
                                parent.Tell(
                                    new AggregatedData(
                                        new List<Restaurant>(
                                            _restaurants)));
                            });
            });
        }

        protected override void PostStop()
        {
            _subscription?.Dispose();
            base.PostStop();
        }
    }
}