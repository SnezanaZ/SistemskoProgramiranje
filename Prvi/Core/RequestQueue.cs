using System.Collections.Generic;
using System.Net;
using System.Threading;

public class RequestQueue
{
    private Queue<HttpListenerContext> queue = new();
    private object lockObj = new();
    public void Enqueue(HttpListenerContext c)
    {
        lock (lockObj)
        {
            queue.Enqueue(c);
            Monitor.Pulse(lockObj);
        }
    }
    public HttpListenerContext Dequeue(Func<bool> running)
    {
        lock (lockObj)
        {
            while (queue.Count == 0 && running())
                Monitor.Wait(lockObj);

            if (queue.Count == 0) return null;
            return queue.Dequeue();
        }
    }

}