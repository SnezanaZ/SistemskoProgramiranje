
using System.Collections.Generic;

namespace Treci
{
    // Web server → ManagerActor: korisnik traži podatke za lokaciju
    public record FetchRequest(string Location);

    // Rx stream → StateActor: novi batch podataka stigao sa periodičnog pollinga
    public record RestaurantBatch(string Location, List<Restaurant> Restaurants);

    // StateActor → SortActor (interno): agregirani podaci pre sortiranja
    public record AggregatedData(List<Restaurant> Restaurants);

    // SortActor → StateActor: finalno sortirani podaci, spremni za čuvanje
    public record SortedData(List<Restaurant> Restaurants);

    // ManagerActor → StateActor: web zahtev za trenutno keširano stanje
    public record GetCachedData(string Location);

    // StateActor → web server (odgovor na GetCachedData)
    public record CachedDataResponse(string Location, List<Restaurant> Restaurants, bool IsReady);

    // ManagerActor → RxCoordinatorActor: pokreni polling za lokaciju
    public record StartPolling(string Location);

    public record StopPolling(string Location);
}
