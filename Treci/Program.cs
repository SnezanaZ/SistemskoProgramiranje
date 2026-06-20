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
        private static ActorSystem system;
        private static IActorRef manager;

        static async Task Main(string[] args)
        {
            system = ActorSystem.Create(
                "YelpSystem",
                SystemConfig.GetAkkaConfig());

            manager = system.ActorOf(
                Props.Create(() => new ManagerActor())
                     .WithDispatcher("yelp-dispatcher"),
                "manager");

            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/restaurants/");
            listener.Start();

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] SERVER STARTED");
            Console.WriteLine(
                "Open: http://localhost:8080/restaurants/?location=Belgrade");

            try
            {
                while (true)
                {
                    var context = await listener.GetContextAsync();

                    _ = Task.Run(() =>
                        HandleRequest(context));
                }
            }
            finally
            {
                listener.Stop();
                await system.Terminate();
            }
        }

        private static async Task HandleRequest(
            HttpListenerContext ctx)
        {
            var location =
                ctx.Request.QueryString["location"];

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] " +
                $"REQUEST | Thread: {Thread.CurrentThread.ManagedThreadId} | " +
                $"Location: {location}");

            if (string.IsNullOrWhiteSpace(location))
            {
                ctx.Response.StatusCode =
                    (int)HttpStatusCode.BadRequest;

                var bytes = Encoding.UTF8.GetBytes(
                    "Query parameter 'location' is required.");

                await ctx.Response.OutputStream
                    .WriteAsync(bytes, 0, bytes.Length);

                ctx.Response.Close();
                return;
            }

            try
            {
                var result =
                    await manager.Ask<SortedData>(
                        new FetchRequest(location),
                        TimeSpan.FromSeconds(20));

                var json =
                    JsonConvert.SerializeObject(
                        result.Restaurants,
                        Formatting.Indented);

                var bytes =
                    Encoding.UTF8.GetBytes(json);

                ctx.Response.StatusCode =
                    (int)HttpStatusCode.OK;
                ctx.Response.ContentType =
                    "application/json";

                await ctx.Response.OutputStream
                    .WriteAsync(bytes, 0, bytes.Length);

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] " +
                    $"SUCCESS | Returned " +
                    $"{result.Restaurants.Count} restaurants.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] " +
                    $"ERROR | {ex.Message}");

                ctx.Response.StatusCode =
                    (int)HttpStatusCode.InternalServerError;

                var bytes =
                    Encoding.UTF8.GetBytes(
                        $"Server error: {ex.Message}");

                await ctx.Response.OutputStream
                    .WriteAsync(bytes, 0, bytes.Length);
            }
            finally
            {
                ctx.Response.Close();
            }
        }
    }
}