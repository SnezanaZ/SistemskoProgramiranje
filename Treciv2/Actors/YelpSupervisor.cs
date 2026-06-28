using System;
using Akka.Actor;
using Akka.Event;

namespace Treciv2
{
    public class YelpSupervisor : UntypedActor
    {
        private readonly TimeSpan _pollInterval;

        public YelpSupervisor(TimeSpan pollInterval)
        {
            _pollInterval = pollInterval;
        }

        protected ILoggingAdapter Log { get; } = Context.GetLogger();

        protected override void PreStart()
        {
            Log.Info("Yelp application started.");
        }

        protected override void PostStop()
        {
            Log.Info("Yelp application stopped.");
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case "start":

                    var stateActor = Context.ActorOf(
                        Akka.Actor.Props.Create(() => new StateActor())
                             .WithDispatcher("yelp-dispatcher"),
                        "state");

                    var rxCoordinator = Context.ActorOf(
                        Akka.Actor.Props.Create(() => new RxCoordinatorActor(stateActor, _pollInterval))
                             .WithDispatcher("yelp-dispatcher"),
                        "rx-coordinator");

                    var managerActor = Context.ActorOf(
                        Akka.Actor.Props.Create(() => new ManagerActor(stateActor, rxCoordinator))
                             .WithDispatcher("yelp-dispatcher"),
                        "manager");

                    Sender.Tell(managerActor);
                    break;
            }
        }

        public static Props Props(TimeSpan pollInterval) =>
            Akka.Actor.Props.Create(() => new YelpSupervisor(pollInterval));
    }
}