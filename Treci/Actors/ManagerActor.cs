using System;
using System.Threading;
using Akka.Actor;

namespace Treci
{
    /// <summary>
    /// Prima web zahteve (FetchRequest) i prevodi ih u dve akcije:
    /// 1. Osigurava da Rx polling teče za tu lokaciju (StartPolling → RxCoordinator)
    /// 2. Traži trenutno keširano stanje od StateActor-a i vraća ga web serveru
    /// 
    /// Manager NIKADA direktno ne poziva API — samo koordinira aktore.
    /// </summary>
    public class ManagerActor : ReceiveActor
    {
        private readonly IActorRef _stateActor;
        private readonly IActorRef _rxCoordinator;
        private int _totalRequestsHandled = 0;

        public ManagerActor(IActorRef stateActor, IActorRef rxCoordinator)
        {
            _stateActor = stateActor;
            _rxCoordinator = rxCoordinator;

            Receive<FetchRequest>(req =>
            {
                _totalRequestsHandled++;
                var sender = Sender;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] MANAGER ACTOR | Request #{_totalRequestsHandled} | " +
                    $"Location: {req.Location} | Thread: {Thread.CurrentThread.ManagedThreadId}");

                // 1. Osiguraj da polling teče za ovu lokaciju
                //    Ako već teče, RxCoordinator će ignorisati duplikat
                _rxCoordinator.Tell(new StartPolling(req.Location));

                // 2. Traži trenutno keširano stanje — odgovor stiže kao CachedDataResponse
                _stateActor
                    .Ask<CachedDataResponse>(new GetCachedData(req.Location), TimeSpan.FromSeconds(5))
                    .PipeTo(sender);
            });
        }

        protected override void PostStop()
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] MANAGER ACTOR | Stopped | Total requests: {_totalRequestsHandled}");
            base.PostStop();
        }
    }
}