using System.ComponentModel;
using System.Reflection.Metadata;

class Program
{
    static void Main()
    {
        var cache=new ImageCache(100);
        var queue=new RequestQueue();
        var converter=new ImageConverter();
        var resolver=new FileResolver("root/images");
        var logger=new Logger();

        var server=new HttpImageServer(queue,logger);
        bool running=true;

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel=true;
            running=false;
            Console.WriteLine("Zaustavljen server....");
            server.Stop();
        };

        for(int i = 0; i < 4; i++)
        {
            new Thread(() =>
            {
                var radnik=new Worker(queue, cache, converter, resolver, logger, () => running);
                radnik.Run();
            }).Start();
        }
        server.Start(()=>running);

    }

}