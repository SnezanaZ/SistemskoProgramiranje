using Akka.Actor;

namespace Treci
{
    public class ManagerActor : ReceiveActor
    {
        public ManagerActor()
        {
            Receive<FetchRequest>(req =>
            {
                var locationActor =
                    Context.ActorOf(
                        Props.Create(() =>
                            new LocationActor(
                                req.Location))
                        .WithDispatcher(
                            "yelp-dispatcher"));

                locationActor.Forward(req);
            });
        }
    }
}