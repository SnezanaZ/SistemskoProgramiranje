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
        try
        {
            
            HttpListenerContext ctx = await listener.GetContextAsync();

            _ = ProcessRequestWithThrottleAsync(ctx);
        }
        catch (Exception ex)
        {
            
            if (running())
                logger.Log($"Greška pri prihvatanju konekcije: {ex.Message}");
            break;
        }
    }

    logger.Log("Server prestao da prima zahteve.");
    }
private async Task ProcessRequestWithThrottleAsync(HttpListenerContext ctx)
{
    try
    {
        
        await semaphore.WaitAsync();

        Worker worker = new Worker(cache, converter, resolver, logger);
        await worker.ProcessAsync(ctx);

        logger.Log("Zahtev uspešno obrađen.");
    }
    catch (Exception ex)
    {
        logger.Log("Greška u obradi zahteva: " + ex.GetBaseException().Message);
    }
    finally
    {
       
        semaphore.Release();
    }
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