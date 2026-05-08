class Program
{
    private static volatile bool running = true;

    static void Main()
    {
        var cache = new ImageCache(4);
        var converter = new ImageConverter();
        var resolver = new FileResolver("root/images");
        var logger = new Logger();

        var server = new HttpImageServer(cache, converter, resolver, logger);

        Thread inputThread = new Thread(() => {
            Console.WriteLine("Server pokrenut. Pritisnite 'Q' da biste ugasili server");
            while (running) {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) {
                    running = false;
                    server.Stop(); 
                    break;
                }
            }
        });
        inputThread.Start();

        server.Start(() => running);
        Console.WriteLine("Sistem je uspesno zaustavljen.");
    }
}