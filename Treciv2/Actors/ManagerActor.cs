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
                // Pokreni Rx polling ako već nije pokrenut
                _rxCoordinator.Tell(new StartPolling(req.Location));

                // Prosledi zahtev StateActoru i vrati odgovor direktno senderu
                _stateActor
                    .Ask<CachedDataResponse>(new GetCachedData(req.Location), TimeSpan.FromSeconds(5))
                    .PipeTo(Sender);
            });
        }
    }
}