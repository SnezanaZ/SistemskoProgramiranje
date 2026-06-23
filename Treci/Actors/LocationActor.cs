using System.Threading;
using Akka.Actor;

namespace Treci
{
    public class LocationActor : ReceiveActor
    {
        private IActorRef _originalSender;
        private IActorRef _sortActor;
        private readonly string _location;

        public LocationActor(string location)
        {
            _location = location;

            Receive<FetchRequest>(req =>
            {
                _originalSender = Sender;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] LOCATION ACTOR | Handling location: {_location} | Thread: {Thread.CurrentThread.ManagedThreadId}");

                var fetchActor = Context.ActorOf(
                    Props.Create(() => new FetchActor())
                         .WithDispatcher("yelp-dispatcher"),
                    $"fetch-{_location.Replace(" ", "_")}");

                fetchActor.Tell(req, Self);
            });

            Receive<AggregatedData>(data =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] LOCATION ACTOR | Aggregation done for: {_location} | " +
                    $"Count: {data.Restaurants.Count} | Thread: {Thread.CurrentThread.ManagedThreadId}");

                _sortActor = Context.ActorOf(
                    Props.Create(() => new SortActor())
                         .WithDispatcher("yelp-dispatcher"),
                    $"sort-{_location.Replace(" ", "_")}");

                // Self kao sender — SortActor odgovara LocationActor-u
                _sortActor.Tell(data, Self);
            });

            Receive<SortedData>(data =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] LOCATION ACTOR | Sending sorted results to client | " +
                    $"Count: {data.Restaurants.Count}");

                _originalSender.Tell(data);
                
                Context.Stop(_sortActor);
                Context.Stop(Self);
            });

            Receive<Status.Failure>(f =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] LOCATION ACTOR | Failure for: {_location} | Error: {f.Cause.Message}");

                _originalSender.Tell(f);
                Context.Stop(Self);
            });
        }

        protected override void PostStop()
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] LOCATION ACTOR | Stopped for: {_location}");
            _sortActor?.Tell(PoisonPill.Instance);
            base.PostStop();
        }
    }
}