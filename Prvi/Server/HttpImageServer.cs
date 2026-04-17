using System.Net;

public class HttpImageServer
{
    private readonly HttpListener listener=new ();
    private readonly RequestQueue queue;
    private readonly Logger logger;

    public HttpImageServer(RequestQueue queue,Logger logger)
    {
        this.queue=queue;
        this.logger=logger;
        listener.Prefixes.Add("http://localhost:5050/");
    }
    public void Start(Func<bool> running)
    {
        listener.Start();
        logger.Log("Server poceo");
        while (running())
        {
            try
            {
                var ctx=listener.GetContext();
                queue.Enqueue(ctx);
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