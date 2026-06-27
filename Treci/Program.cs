using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Newtonsoft.Json;

namespace Treci
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var system = ActorSystem.Create("YelpSystem", SystemConfig.GetAkkaConfig());

            var stateActor = system.ActorOf(
                Props.Create(() => new StateActor())
                     .WithDispatcher("yelp-dispatcher"),
                "state");

            var pollInterval = TimeSpan.FromMinutes(2);
            var rxCoordinator = system.ActorOf(
                Props.Create(() => new RxCoordinatorActor(stateActor, pollInterval))
                     .WithDispatcher("yelp-dispatcher"),
                "rx-coordinator");
            rxCoordinator.Tell(new StartPolling("Belgrade"));
            var manager = system.ActorOf(
                Props.Create(() => new ManagerActor(stateActor, rxCoordinator))
                     .WithDispatcher("yelp-dispatcher"),
                "manager");

            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/restaurants/");
            listener.Start();

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STARTED");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Listening on: http://localhost:8080/restaurants/");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Example: http://localhost:8080/restaurants/?location=Belgrade");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Poll interval: {pollInterval.TotalMinutes} min");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Type 'q' to stop.");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========================================");

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SHUTDOWN SIGNAL (Ctrl+C)");
                cts.Cancel();
                listener.Stop();
            };

            _ = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var line = Console.ReadLine();
                    if (line?.Equals("q", StringComparison.OrdinalIgnoreCase) == true ||
                        line?.Equals("exit", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SHUTDOWN SIGNAL ('q')");
                        cts.Cancel();
                        listener.Stop();
                        break;
                    }
                }
            });

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync();

                    _ = HandleRequest(context, manager).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss}] UNHANDLED REQUEST ERROR | " +
                                $"{t.Exception?.GetBaseException().Message}");
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (HttpListenerException) when (cts.Token.IsCancellationRequested) { }
            finally
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STOPPING...");
                listener.Close();
                await system.Terminate();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STOPPED. Goodbye.");
            }
        }

        private static async Task HandleRequest(HttpListenerContext ctx, IActorRef manager)
        {
            var location = ctx.Request.QueryString["location"];
            var requestId = Guid.NewGuid().ToString("N")[..8];

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ── REQUEST [{requestId}] ──────────────────────");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Method: {ctx.Request.HttpMethod} | URL: {ctx.Request.Url} | Thread: {Thread.CurrentThread.ManagedThreadId}");

            try
            {
                if (string.IsNullOrWhiteSpace(location))
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    ctx.Response.ContentType = "application/json";
                    var errBytes = Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(new { error = "Query parameter 'location' is required." }));
                    await ctx.Response.OutputStream.WriteAsync(errBytes, 0, errBytes.Length);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] BAD REQUEST [{requestId}] | Missing 'location' parameter");
                    return;
                }

                var startTime = DateTime.Now;

                var result = await manager.Ask<CachedDataResponse>(
                    new FetchRequest(location),
                    TimeSpan.FromSeconds(5));

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

                if (!result.IsReady)
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                    {
                        location,
                        message = "Data is being fetched, please retry in a few seconds.",
                        isReady = false
                    }));
                    await ctx.Response.OutputStream.WriteAsync(body, 0, body.Length);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] NOT READY [{requestId}] | Location: {location} | Duration: {elapsed:F0}ms");
                }
                else
                {
                    var json = JsonConvert.SerializeObject(new
                    {
                        location,
                        count = result.Restaurants.Count,
                        restaurants = result.Restaurants
                    }, Formatting.Indented);

                    var bytes = Encoding.UTF8.GetBytes(json);
                    ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SUCCESS [{requestId}] | Returned {result.Restaurants.Count} restaurants | Duration: {elapsed:F0}ms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR [{requestId}] | {ex.GetType().Name}: {ex.Message}");
                try
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    ctx.Response.ContentType = "application/json";
                    var errBytes = Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(new { error = ex.Message }));
                    await ctx.Response.OutputStream.WriteAsync(errBytes, 0, errBytes.Length);
                }
                catch { /* response možda već zatvoren */ }
            }
            finally
            {
                ctx.Response.Close();
            }
        }
    }
}