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
        private static ActorSystem _system;
        private static IActorRef _manager;

        static async Task Main(string[] args)
        {
            _system = ActorSystem.Create("YelpSystem", SystemConfig.GetAkkaConfig());

            _manager = _system.ActorOf(
                Props.Create(() => new ManagerActor())
                     .WithDispatcher("yelp-dispatcher"),
                "manager");

            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/restaurants/");
            listener.Start();

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STARTED");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Listening on: http://localhost:8080/restaurants/");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Example: http://localhost:8080/restaurants/?location=Belgrade");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Type 'exit' or press Ctrl+C to stop.");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========================================");

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SHUTDOWN SIGNAL RECEIVED (Ctrl+C)");
                cts.Cancel();
                listener.Stop();
            };

            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var line = Console.ReadLine();
                    if (line?.Equals("exit", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SHUTDOWN SIGNAL RECEIVED (exit command)");
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
                    _ = HandleRequest(context);
                }
            }
            catch (HttpListenerException) when (cts.Token.IsCancellationRequested)
            {
                
            }
            finally
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STOPPING...");
                listener.Close();
                await _system.Terminate();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STOPPED. Goodbye.");
            }
        }

        private static async Task HandleRequest(HttpListenerContext ctx)
        {
            var location = ctx.Request.QueryString["location"];
            var requestId = Guid.NewGuid().ToString("N")[..8];

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] ── REQUEST [{requestId}] ──────────────────────");
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] Method: {ctx.Request.HttpMethod} | " +
                $"URL: {ctx.Request.Url} | Thread: {Thread.CurrentThread.ManagedThreadId}");
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] Location param: '{location}'");

            if (string.IsNullOrWhiteSpace(location))
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] BAD REQUEST [{requestId}] — missing 'location' parameter");

                ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                var errBytes = Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new { error = "Query parameter 'location' is required." }));
                ctx.Response.ContentType = "application/json";
                await ctx.Response.OutputStream.WriteAsync(errBytes, 0, errBytes.Length);
                ctx.Response.Close();
                return;
            }

            var startTime = DateTime.Now;

            try
            {
                var result = await _manager.Ask<SortedData>(
                    new FetchRequest(location),
                    TimeSpan.FromSeconds(20));

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

                var json = JsonConvert.SerializeObject(new
                {
                    location = location,
                    count = result.Restaurants.Count,
                    restaurants = result.Restaurants
                }, Formatting.Indented);

                var bytes = Encoding.UTF8.GetBytes(json);

                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] SUCCESS [{requestId}] | " +
                    $"Returned {result.Restaurants.Count} restaurants | " +
                    $"Duration: {elapsed:F0}ms");
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] ERROR [{requestId}] | " +
                    $"{ex.GetType().Name}: {ex.Message} | Duration: {elapsed:F0}ms");

                ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                var errBytes = Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new { error = ex.Message }));
                ctx.Response.ContentType = "application/json";
                await ctx.Response.OutputStream.WriteAsync(errBytes, 0, errBytes.Length);
            }
            finally
            {
                ctx.Response.Close();
            }
        }
    }
}