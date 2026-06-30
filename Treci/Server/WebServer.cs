// WebServer.cs
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Newtonsoft.Json;

namespace Treci
{
    public class WebServer
    {
        private readonly HttpListener _listener = new();
        private readonly IActorRef _manager;
        private readonly string _prefix;

        public WebServer(IActorRef manager, string prefix = "http://localhost:8080/restaurants/")
        {
            _manager = manager;
            _prefix = prefix;
            _listener.Prefixes.Add(prefix);
        }
        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========================================");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STARTED");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Listening on: {_prefix}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Example: {_prefix}?location=Belgrade");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========================================");
        }

        public void Stop() => _listener.Stop();
        public void Close() => _listener.Close();


        public async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var context = await _listener.GetContextAsync();

                    _ = HandleRequest(context).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss}] UNHANDLED REQUEST ERROR | " +
                                $"{t.Exception?.GetBaseException().Message}");
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (HttpListenerException) when (token.IsCancellationRequested) { }
        }

        private async Task HandleRequest(HttpListenerContext ctx)
        {
            var location = ctx.Request.QueryString["location"];
            var requestId = Guid.NewGuid().ToString("N")[..8];

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ── REQUEST [{requestId}] ──────────────────────");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Method: {ctx.Request.HttpMethod} | URL: {ctx.Request.Url} | Thread: {Thread.CurrentThread.ManagedThreadId}");

            try
            {
                if (string.IsNullOrWhiteSpace(location))
                {
                    await WriteJson(ctx, HttpStatusCode.BadRequest,
                        new { error = "Query parameter 'location' is required." });
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] BAD REQUEST [{requestId}] | Missing 'location' parameter");
                    return;
                }

                var startTime = DateTime.Now;
                var result = await _manager.Ask<CachedDataResponse>(
                    new FetchRequest(location),
                    TimeSpan.FromSeconds(8));
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

                if (!result.IsReady)
                {
                    await WriteJson(ctx, HttpStatusCode.ServiceUnavailable, new
                    {
                        location,
                        message = "Data is being fetched, please retry in a few seconds.",
                        isReady = false
                    });
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] NOT READY [{requestId}] | Location: {location} | Duration: {elapsed:F0}ms");
                }
                else
                {
                    await WriteJson(ctx, HttpStatusCode.OK, new
                    {
                        location,
                        count = result.Restaurants.Count,
                        restaurants = result.Restaurants
                    }, indented: true);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SUCCESS [{requestId}] | Returned {result.Restaurants.Count} restaurants | Duration: {elapsed:F0}ms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR [{requestId}] | {ex.GetType().Name}: {ex.Message}");
                try
                {
                    await WriteJson(ctx, HttpStatusCode.InternalServerError, new { error = ex.Message });
                }
                catch { }
            }
            finally
            {
                ctx.Response.Close();
            }
        }

        private static async Task WriteJson(HttpListenerContext ctx, HttpStatusCode status, object payload, bool indented = false)
        {
            var json = JsonConvert.SerializeObject(payload, indented ? Formatting.Indented : Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = (int)status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}