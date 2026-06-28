using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;

namespace Treciv2
{
    public class YelpApp
    {
        public static async Task Init()
        {
            var system = ActorSystem.Create("YelpSystem", SystemConfig.GetAkkaConfig());

            var supervisor =
                system.ActorOf(YelpSupervisor.Props(TimeSpan.FromMinutes(2)), "supervisor");

            var manager =
                await supervisor.Ask<IActorRef>("start");
            manager.Tell(new FetchRequest("Belgrade"));
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/restaurants/");
            listener.Start();

            Console.WriteLine("================================");
            Console.WriteLine("SERVER STARTED");
            Console.WriteLine("Type 'q' to stop server");
            Console.WriteLine("================================");

            var cts = new CancellationTokenSource();

            // =========================
            // CTRL + C SHUTDOWN
            // =========================
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("CTRL+C shutdown triggered");

                cts.Cancel();
                listener.Stop();
            };

            // =========================
            // 'q' SHUTDOWN
            // =========================
            _ = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var input = Console.ReadLine();

                    if (input?.Equals("q", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Console.WriteLine("'q' shutdown triggered");

                        cts.Cancel();
                        listener.Stop();
                        break;
                    }
                }
            });

            // =========================
            // HTTP LOOP
            // =========================
            var serverTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    HttpListenerContext ctx;

                    try
                    {
                        ctx = await listener.GetContextAsync();
                    }
                    catch
                    {
                        break; // listener stopped
                    }

                    _ = HandleRequest(ctx, manager);
                }
            });

            // =========================
            // WAIT FOR SHUTDOWN
            // =========================
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(200);
            }

            Console.WriteLine("SHUTTING DOWN...");

            try
            {
                listener.Close();
            }
            catch { }

            await system.Terminate();

            Console.WriteLine("SHUTDOWN COMPLETE");
        }

        // =========================
        // HTTP HANDLER
        // =========================
        private static async Task HandleRequest(HttpListenerContext ctx, IActorRef manager)
        {
            try
            {
                var location = ctx.Request.QueryString["location"];

                if (string.IsNullOrWhiteSpace(location))
                {
                    ctx.Response.StatusCode = 400;
                    var err = Encoding.UTF8.GetBytes("Missing location");
                    await ctx.Response.OutputStream.WriteAsync(err);
                    ctx.Response.Close();
                    return;
                }

                var result = await manager.Ask<CachedDataResponse>(
                    new FetchRequest(location),
                    TimeSpan.FromSeconds(5));

                    if (!result.IsReady || result.Restaurants.Count == 0)
{
    ctx.Response.StatusCode = 202; // ACCEPTED
    var bytess = Encoding.UTF8.GetBytes(
        "Data is loading, try again in 1-2 seconds");

    await ctx.Response.OutputStream.WriteAsync(bytess);
    ctx.Response.Close();
    return;
}

                var json = System.Text.Json.JsonSerializer.Serialize(result);

                var bytes = Encoding.UTF8.GetBytes(json);

                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";

                await ctx.Response.OutputStream.WriteAsync(bytes);

                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");

                try
                {
                    ctx.Response.StatusCode = 500;
                    var err = Encoding.UTF8.GetBytes(ex.Message);
                    await ctx.Response.OutputStream.WriteAsync(err);
                    ctx.Response.Close();
                }
                catch { }
            }
        }
    }
}