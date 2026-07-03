using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akka.Actor;

namespace Treci
{
    public class SortActor : ReceiveActor
    {
        private List<Restaurant> _lastSortedResults = new();

        public SortActor()
        {
            Receive<AggregatedData>(data =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Received {data.Restaurants.Count} restaurants | " +
                    $"Thread: {Thread.CurrentThread.ManagedThreadId}");

                _lastSortedResults = data.Restaurants
                    .OrderByDescending(r => r.PriceLevel)
                    .ThenByDescending(r => r.Rating)
                    .ToList();

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Sorting complete | " +
                    $"Top: {_lastSortedResults.FirstOrDefault()?.Name ?? "none"}");

                Sender.Tell(new SortedData(new List<Restaurant>(_lastSortedResults)));
                Context.Stop(Self);
            });
        }

        protected override void PostStop()
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Stopped | Last count: {_lastSortedResults.Count}");
            base.PostStop();
        }
    }
}