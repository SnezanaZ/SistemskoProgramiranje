using System.Diagnostics;
using System.Net;

public class Worker
{
    private readonly RequestQueue queue;
    private readonly ImageCache cache;
    private readonly ImageConverter converter;
    private readonly FileResolver resolver;
    private readonly Logger logger;
    private readonly Func<bool> running;

    public Worker(RequestQueue q, ImageCache c,ImageConverter ic,FileResolver fr,Logger l, Func<bool> running)
    {
        queue=q;
        cache=c;
        converter=ic;
        resolver=fr;
        logger=l;
        this.running=running;
    }
    public void Run()
    {
        while (running())
        {
            var context=queue.Dequeue();
            Process(context);
        }
    }
    private void Process(HttpListenerContext ctx)
    {
        try
        {
            string file=ctx.Request.RawUrl.TrimStart('/');
            string path=resolver.Resolve(file);
            if (!File.Exists(path))
            {
                ctx.Response.StatusCode=404;
                ctx.Response.Close();
                return;
            }
            var data=cache.GetOrAdd(file,()=>converter.Convert(path));
            ctx.Response.ContentType="image/png";
            ctx.Response.OutputStream.Write(data,0,data.Length);
            ctx.Response.Close();
            logger.Log($"Processed {file}");
            
        }catch(Exception ex)
        {
            logger.Log("ERROR: " + ex.Message);
        }
    }

}