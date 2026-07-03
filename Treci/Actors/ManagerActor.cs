// using System;
// using System.Threading;
// using Akka.Actor;

// namespace Treci
// {
//     public class ManagerActor : ReceiveActor
//     {
//         private readonly IActorRef _stateActor;
//         private readonly IActorRef _rxCoordinator;
//         private int _totalRequestsHandled = 0;

//         public ManagerActor(IActorRef stateActor, IActorRef rxCoordinator)
//         {
//             _stateActor = stateActor;
//             _rxCoordinator = rxCoordinator;

//             Receive<FetchRequest>(req =>
//             {
//                 _totalRequestsHandled++;
//                 var sender = Sender;

//                 Console.WriteLine(
//                     $"[{DateTime.Now:HH:mm:ss}] MANAGER ACTOR | Request #{_totalRequestsHandled} | " +
//                     $"Location: {req.Location} | Thread: {Thread.CurrentThread.ManagedThreadId}");

//                 _rxCoordinator.Tell(new StartPolling(req.Location));

//                 _stateActor
//                     .Ask<CachedDataResponse>(new GetCachedData(req.Location), TimeSpan.FromSeconds(5))
//                     .PipeTo(sender);
//             });
//         }

//         protected override void PostStop()
//         {
//             Console.WriteLine(
//                 $"[{DateTime.Now:HH:mm:ss}] MANAGER ACTOR | Stopped | Total requests: {_totalRequestsHandled}");
//             base.PostStop();
//         }
//     }
// }