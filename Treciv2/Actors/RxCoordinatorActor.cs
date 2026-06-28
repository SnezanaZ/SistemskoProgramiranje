using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Treciv2
{
    public class RxCoordinatorActor : ReceiveActor
    {
        private readonly YelpRxService _service = new();
        private readonly IActorRef _stateActor;
        private readonly TimeSpan _interval;

        private readonly Dictionary<string, IDisposable> _subs = new();

        public RxCoordinatorActor(IActorRef stateActor, TimeSpan interval)
        {
            _stateActor = stateActor;
            _interval = interval;

            Receive<StartPolling>(msg =>
            {
                if (_subs.ContainsKey(msg.Location))
                    return;

                var sub = _service
                    .PollRestaurantsPeriodically(msg.Location, _interval)
                    .Subscribe(batch =>
                    {
                        _stateActor.Tell(batch);
                    });

                _subs[msg.Location] = sub;
            });

            Receive<StopPolling>(msg =>
            {
                if (_subs.TryGetValue(msg.Location, out var sub))
                {
                    sub.Dispose();
                    _subs.Remove(msg.Location);
                }
            });
        }

        protected override void PostStop()
        {
            foreach (var sub in _subs.Values)
                sub.Dispose();

            base.PostStop();
        }
    }
}