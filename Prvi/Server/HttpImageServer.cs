using System.Net;

public class HttpImageServer
{
    private readonly HttpListener listener = new();
    private readonly RequestQueue queue;
    private readonly Logger logger;

    // dodato
    // private readonly ImageCache cache;
    // private readonly ImageConverter converter;
    // private readonly FileResolver resolver;

    public HttpImageServer(RequestQueue queue, Logger logger)
    {
        this.queue = queue;
        this.logger = logger;
        listener.Prefixes.Add("http://localhost:5050/");
    }

    // dodato 
    // public HttpImageServer(ImageCache c, ImageConverter ic, FileResolver fr, Logger l)
    // {
    //     cache = c; converter = ic; resolver = fr; logger = l;
    //     listener.Prefixes.Add("http://localhost:5050/");
    // }
    public void Start(Func<bool> running)
    {
        listener.Start();
        logger.Log("Server poceo");
        while (running())
        {
            try
            {
                var ctx = listener.GetContext();
                queue.Enqueue(ctx);
            //    ThreadPool.QueueUserWorkItem(_ => {
            //         var worker = new Worker(cache, converter, resolver, logger);
            //         worker.Process(ctx);
            //     });
            }
            catch
            {
                break;
            }
            logger.Log("Server je prestao da prima zahteve.");
        }
    }
    public void Stop()
    {
        listener.Stop();
    }
}