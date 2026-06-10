using System.Net;

public class HttpImageServer
{
    private readonly HttpListener listener = new();

    private readonly Logger logger;
    private readonly ImageCache cache;
    private readonly ImageConverter converter;
    private readonly FileResolver resolver;

    private readonly SemaphoreSlim semaphore;

    public HttpImageServer(
        ImageCache cache,
        ImageConverter converter,
        FileResolver resolver,
        Logger logger,
        int maxParallelWorkers = 4)
    {
        this.cache = cache;
        this.converter = converter;
        this.resolver = resolver;
        this.logger = logger;

        semaphore = new SemaphoreSlim(
            maxParallelWorkers,
            maxParallelWorkers);

        listener.Prefixes.Add(
            "http://localhost:5050/");
    }

    public async Task StartAsync(
        Func<bool> running)
    {
        listener.Start();

        logger.Log("Server pokrenut.");

        while (running())
        {
            HttpListenerContext ctx;

            try
            {
                ctx = await listener.GetContextAsync();
            }
            catch
            {
                break;
            }

            await semaphore.WaitAsync();

            var processingTask =
                Task.Run(async () =>
                {
                    try
                    {
                        Worker worker =
                            new Worker(
                                cache,
                                converter,
                                resolver,
                                logger);

                        await worker.ProcessAsync(ctx);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

            _ = processingTask.ContinueWith(t =>
  {
      if (t.IsFaulted)
      {
          logger.Log(
              "Greška u obradi zahteva: "
              + t.Exception?.GetBaseException().Message);
      }
      else
      {
          logger.Log(
              "Zahtev uspešno obrađen.");
      }
  });

    
        }
            logger.Log(
                "Server prestao da prima zahteve.");    
    }

    public void Stop()
    {
        try
        {
            listener.Stop();
        }
        catch
        {
        }
    }
}