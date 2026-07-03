using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;

namespace Treci
{
    public class AppHost
    {
        public async Task RunAsync()
        {
            var system = ActorSystem.Create("YelpSystem", SystemConfig.GetAkkaConfig());

            var pollInterval = TimeSpan.FromMinutes(2);
            var stateActor = system.ActorOf(StateActor.CreateProps(pollInterval), "state");

            var server = new WebServer(stateActor);
            server.Start();

            var cts = new CancellationTokenSource();
            RegisterShutdownHandlers(server, cts);
            await server.RunAsync(cts.Token);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STOPPING...");
            server.Close();
            await system.Terminate();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STOPPED. Goodbye.");
        }

        private static void RegisterShutdownHandlers(WebServer server, CancellationTokenSource cts)
        {
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SHUTDOWN SIGNAL (Ctrl+C)");
                cts.Cancel();
                server.Stop();
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
                        server.Stop();
                        break;
                    }
                }
            });
        }
    }
}