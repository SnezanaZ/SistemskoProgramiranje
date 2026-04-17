using System.Collections.Generic;
using System.Net;
using System.Threading;

public class RequestQueue
{
    private Queue<HttpListenerContext> queue=new();
    private object lockObj=new();
    public void Enqueue(HttpListenerContext c)
    {
        lock(lockObj)
        {
           queue.Enqueue(c);
           Monitor.Pulse(lockObj); 
        }
    }
    public HttpListenerContext Dequeue()
    {
        lock (lockObj)
        {
            while(queue.Count==0)
            Monitor.Wait(lockObj);
            return queue.Dequeue();
        }
    }

}