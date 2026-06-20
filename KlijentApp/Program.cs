using System.Net.Http;
using System.Diagnostics;

class Program
{
    static HttpClient client = new HttpClient();

    static async Task Main()
    {
        string[] locations =
        {
            "London",
            "Madrid",
            "Paris"
        };

        var sw = Stopwatch.StartNew();

        var tasks = locations.Select(async loc =>
{
    var url = $"http://localhost:8080/restaurants?location={loc}";
    var res = await client.GetStringAsync(url);

    return $"{loc} -> {res}";
});

var results = await Task.WhenAll(tasks);

foreach (var r in results)
{
    Console.WriteLine(r);
}

        await Task.WhenAll(tasks);

        sw.Stop();

        Console.WriteLine($"TOTAL: {sw.ElapsedMilliseconds} ms");
    }
}