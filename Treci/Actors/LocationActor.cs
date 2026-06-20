using Akka.Actor;

namespace Treci
{
    public class LocationActor : ReceiveActor
    {
        private IActorRef _originalSender;

        public LocationActor(string location)
        {
            Receive<FetchRequest>(req =>
            {
                _originalSender = Sender;

                var fetchActor =
                    Context.ActorOf(
                        Props.Create(() =>
                            new FetchActor())
                        .WithDispatcher(
                            "yelp-dispatcher"));

                fetchActor.Tell(req, Self);
            });

            Receive<AggregatedData>(data =>
            {
                var sortActor =
                    Context.ActorOf(
                        Props.Create(() =>
                            new SortActor())
                        .WithDispatcher(
                            "yelp-dispatcher"));

                sortActor.Tell(data);
            });

            Receive<SortedData>(data =>
            {
                _originalSender.Tell(data);
                Context.Stop(Self);
            });

            Receive<Status.Failure>(f =>
            {
                _originalSender.Tell(f);
                Context.Stop(Self);
            });
        }
    }
}