
using System;
using System.Collections.Generic;
using System.Threading;
using Akka.Actor;

namespace Treci
{
    /// <summary>
    /// Centralni aktor koji čuva interno stanje — keširane, sortirane liste
    /// restorana po lokaciji. Ažurira se isključivo kroz RestaurantBatch poruke
    /// koje dolaze od Rx streama, nezavisno od web zahteva.
    ///
    /// Web zahtevi samo čitaju ovo stanje (GetCachedData), ne pokreću API pozive.
    /// 
    /// ISPRAVKA: _pendingSort memory leak — ako SortActor crashuje pre odgovora,
    /// unos bi ostao zauvek. Rešenje: Watch(sortActor) + Terminated handler koji
    /// čisti unos ako SortedData nikad nije stigao.
    /// </summary>
    public class StateActor : ReceiveActor
    {
        // Interno stanje: lokacija → poslednja sortirana lista restorana
        private readonly Dictionary<string, List<Restaurant>> _cache = new();
        private readonly Dictionary<string, DateTime> _lastUpdated = new();

        // Pratimo koji SortActor obrađuje koju lokaciju
        private readonly Dictionary<IActorRef, string> _pendingSort = new();

        // Pratimo koji SortActori su već odgovorili (da razlikujemo crash od završetka)
        private readonly HashSet<IActorRef> _completedSort = new();

        public StateActor()
        {
            // Stigao novi batch od Rx streama — delegiraj sortiranje
            Receive<RestaurantBatch>(batch =>
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | Received batch for: {batch.Location} | " +
                    $"Count: {batch.Restaurants.Count} | Thread: {Thread.CurrentThread.ManagedThreadId}");

                var sortActor = Context.ActorOf(
                    Props.Create(() => new SortActor())
                         .WithDispatcher("yelp-dispatcher"),
                    $"sort-{Guid.NewGuid():N}");

                // Zapamti koji SortActor obrađuje koju lokaciju
                _pendingSort[sortActor] = batch.Location;

                // ISPRAVKA: Watch SortActor-a da bi Terminated stigao ako crashuje
                Context.Watch(sortActor);

                sortActor.Tell(new AggregatedData(batch.Restaurants), Self);
            });

            // SortActor vratio sortirane podatke — sačuvaj u interno stanje
            Receive<SortedData>(data =>
            {
                if (!_pendingSort.TryGetValue(Sender, out var location))
                {
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | SortedData od nepoznatog SortActor-a — ignorišem");
                    return;
                }

                // Označi kao uspešno završen pre uklanjanja iz pending mape
                _completedSort.Add(Sender);
                _pendingSort.Remove(Sender);

                _cache[location] = data.Restaurants;
                _lastUpdated[location] = DateTime.Now;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | Cache updated | " +
                    $"Location: {location} | Count: {data.Restaurants.Count} | " +
                    $"Time: {_lastUpdated[location]:HH:mm:ss}");
            });

            // ISPRAVKA: Hvatamo Terminated signal od watchovanog SortActor-a.
            // Ako je već završio normalno (_completedSort), ignorišemo.
            // Ako nije — znači da je crashovao pre odgovora: čistimo _pendingSort.
            Receive<Terminated>(t =>
            {
                if (_completedSort.Remove(t.ActorRef))
                {
                    // Normalan završetak — Context.Stop u SortActor-u okida Terminated
                    return;
                }

                if (_pendingSort.TryGetValue(t.ActorRef, out var location))
                {
                    _pendingSort.Remove(t.ActorRef);
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | SortActor crashed before reply | " +
                        $"Location: {location} — pending entry cleaned up");
                }
            });

            // Web server traži trenutno keširano stanje — odgovori odmah
            Receive<GetCachedData>(req =>
            {
                var sender = Sender;
                var isReady = _cache.ContainsKey(req.Location);
                var restaurants = isReady
                    ? new List<Restaurant>(_cache[req.Location])
                    : new List<Restaurant>();

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | GetCachedData | " +
                    $"Location: {req.Location} | Ready: {isReady} | Count: {restaurants.Count}" +
                    (isReady ? $" | Last updated: {_lastUpdated[req.Location]:HH:mm:ss}" : " | Not yet cached"));

                sender.Tell(new CachedDataResponse(req.Location, restaurants, isReady));
            });
        }

        protected override void PostStop()
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] STATE ACTOR | Stopped | " +
                $"Cached locations: {string.Join(", ", _cache.Keys)}");
            base.PostStop();
        }
    }
}
