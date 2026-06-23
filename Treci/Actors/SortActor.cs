using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akka.Actor;

namespace Treci
{
    public class SortActor : ReceiveActor
    {
        // Interno stanje — čuva poslednje sortirane rezultate
        private List<Restaurant> _lastSortedResults = new();

        public SortActor()
        {
            Receive<AggregatedData>(data =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Received {data.Restaurants.Count} restaurants to sort | Thread: {Thread.CurrentThread.ManagedThreadId}");

                // Sortiranje po cenovnom rangu opadajuće
                _lastSortedResults = data.Restaurants
                    .OrderByDescending(r => r.PriceLevel)
                    .ThenByDescending(r => r.Rating)   // sekundarni kriterijum
                    .ToList();

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Sorting complete | " +
                    $"Top restaurant: {_lastSortedResults.FirstOrDefault()?.Name ?? "none"}");

                Sender.Tell(new SortedData(new List<Restaurant>(_lastSortedResults)));
            });
        }

        protected override void PostStop()
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Stopped | Last results count: {_lastSortedResults.Count}");
            base.PostStop();
        }
    }
}