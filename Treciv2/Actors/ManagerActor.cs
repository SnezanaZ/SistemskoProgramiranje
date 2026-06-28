using System;
using Akka.Actor;

namespace Treciv2
{
    public class ManagerActor : ReceiveActor
    {
        private readonly IActorRef _stateActor;
        private readonly IActorRef _rxCoordinator;

        public ManagerActor(IActorRef stateActor, IActorRef rxCoordinator)
        {
            _stateActor = stateActor;
            _rxCoordinator = rxCoordinator;

            Receive<FetchRequest>(req =>
{
    var sender = Sender;

    // 1. obavezno pokreni Rx
    _rxCoordinator.Tell(new StartPolling(req.Location));

    // 2. DODAJ KRATKO ČEKANJE DA SE CACHE POPUNI (kljucno)
    Context.System.Scheduler.ScheduleTellOnce(
        TimeSpan.FromSeconds(2),
        _stateActor,
        new GetCachedData(req.Location),
        sender);

    // fallback odmah ako već postoji cache
    _stateActor
        .Ask<CachedDataResponse>(new GetCachedData(req.Location), TimeSpan.FromSeconds(5))
        .PipeTo(sender);
});
        }
    }
}