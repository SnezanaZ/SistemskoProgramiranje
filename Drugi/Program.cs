public class Program
{
    private static volatile bool running = true;

    public static async Task Main()
    {
        var cache = new ImageCache(
            capacity: 50,
            cleanThreshold: 4);

        var converter = new ImageConverter();

        var resolver =
            new FileResolver("root/images");

        var logger = new Logger();

        var server =
            new HttpImageServer(
                cache,
                converter,
                resolver,
                logger,
                maxParallelWorkers: 4);

        Thread inputThread = new Thread(() =>
        {
            Console.WriteLine("Server pokrenut.");
            Console.WriteLine("Pritisnite Q za gašenje.");

            while (running)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.Q)
                    {
                        running = false;

                        server.Stop();

                        break;
                    }
                }

                Thread.Sleep(100);
            }
        });

        inputThread.IsBackground = true;
        inputThread.Start();

        try
        {
            await server.StartAsync(() => running);
        }
        catch (Exception ex)
        {
            logger.Log(
                $"Greška servera: {ex.Message}");
        }
        finally
        {
            await cache.ShutdownAsync();
        }

        Console.WriteLine(
            "Sistem je uspešno zaustavljen.");
    }
}