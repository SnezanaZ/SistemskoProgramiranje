using System.Threading;
using Akka.Actor;

namespace Treci
{
    public class ManagerActor : ReceiveActor
    {
        private int _totalRequestsHandled = 0;

        public ManagerActor()
        {
            Receive<FetchRequest>(req =>
            {
                _totalRequestsHandled++;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] MANAGER ACTOR | Request #{_totalRequestsHandled} | " +
                    $"Location: {req.Location} | Thread: {Thread.CurrentThread.ManagedThreadId}");

                var locationActor = Context.ActorOf(
                    Props.Create(() => new LocationActor(req.Location))
                         .WithDispatcher("yelp-dispatcher"),
                    $"location-{_totalRequestsHandled}-{req.Location.Replace(" ", "_")}");

                locationActor.Forward(req);
            });
        }

        protected override void PostStop()
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] MANAGER ACTOR | Stopped | Total requests handled: {_totalRequestsHandled}");
            base.PostStop();
        }
    }
}