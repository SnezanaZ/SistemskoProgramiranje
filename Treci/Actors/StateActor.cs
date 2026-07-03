using System;
using Akka.Actor;
using Akka.Event;

namespace Treci
{
    public class StateActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly TimeSpan _pollInterval;

        public StateActor(TimeSpan pollInterval)
        {
            _pollInterval = pollInterval;

            Receive<GetCachedData>(req => GetOrCreateChild(req.Location).Forward(req));
        }

        private IActorRef GetOrCreateChild(string location)
        {
            var childName = SanitizeName(location);
            var child = Context.Child(childName);

            if (child.IsNobody())
            {
                _log.Info($"[STATE ACTOR] Kreiranje LocationActor-a za: {location}");

                child = Context.ActorOf(
                    Props.Create(() => new LocationActor(location))
                         .WithDispatcher("yelp-dispatcher"),
                    childName);

                child.Tell(new StartPeriodicFetch(location, _pollInterval));
            }

            return child;
        }

        private static string SanitizeName(string location) =>
            Uri.EscapeDataString(location).Replace("%", "-").ToLowerInvariant();

        public static Props CreateProps(TimeSpan pollInterval) =>
            Props.Create(() => new StateActor(pollInterval))
                .WithDispatcher("yelp-dispatcher");
    }
}