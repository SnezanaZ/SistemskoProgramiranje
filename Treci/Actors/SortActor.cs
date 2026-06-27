using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akka.Actor;

namespace Treci
{
    /// <summary>
    /// Sortira agregirane podatke i vraća ih StateActor-u kao SortedData.
    /// Čuva interno stanje poslednjeg sortiranja za logging/debug.
    /// Kreira se per-batch i zaustavlja se nakon posla.
    /// </summary>
    public class SortActor : ReceiveActor
    {
        // Interno stanje — čuva poslednje sortirane rezultate
        private List<Restaurant> _lastSortedResults = new();

        public SortActor()
        {
            Receive<AggregatedData>(data =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Received {data.Restaurants.Count} restaurants | " +
                    $"Thread: {Thread.CurrentThread.ManagedThreadId}");

                // Sortiranje po cenovnom rangu opadajuće, zatim po ratingu
                _lastSortedResults = data.Restaurants
                    .OrderByDescending(r => r.PriceLevel)
                    .ThenByDescending(r => r.Rating)
                    .ToList();

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Sorting complete | " +
                    $"Top: {_lastSortedResults.FirstOrDefault()?.Name ?? "none"}");

                // Vrati SortedData senderu (StateActor-u)
                Sender.Tell(new SortedData(new List<Restaurant>(_lastSortedResults)));

                // Zaustavi se nakon posla — StateActor kreira novi SortActor za svaki batch
              //  Context.Stop(Self);
            });
        }

        // protected override void PostStop()
        // {
        //     Console.WriteLine(
        //         $"[{DateTime.Now:HH:mm:ss}] SORT ACTOR | Stopped | Last count: {_lastSortedResults.Count}");
        //     base.PostStop();
        // }
    }
}