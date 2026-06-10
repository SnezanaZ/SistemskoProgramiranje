using System.Net;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
public class Worker
{
    private readonly ImageCache cache;
    private readonly ImageConverter converter;
    private readonly FileResolver resolver;
    private readonly Logger logger;

    public Worker(
        ImageCache cache,
        ImageConverter converter,
        FileResolver resolver,
        Logger logger)
    {
        this.cache = cache;
        this.converter = converter;
        this.resolver = resolver;
        this.logger = logger;
    }

    public async Task ProcessAsync(HttpListenerContext ctx)
    {
        try
        {
            string file =
                ctx.Request.RawUrl?
                .TrimStart('/') ?? "";

            string path =
                resolver.Resolve(file);

            if (!File.Exists(path))
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();

                logger.Log(
                    $"Fajl nije pronađen: {file}");

                return;
            }
            // ????
            var imageTask =
    cache.GetOrAddAsync(
        file,
        () => converter.ConvertAsync(path));

            _ = imageTask.ContinueWith(t =>
        {
            if (!t.IsFaulted)
            {
                logger.Log(
                    $"Konverzija završena: {file}");
            }
        });

            byte[] data = await imageTask;

            ctx.Response.ContentType = "image/png";
            ctx.Response.ContentLength64 = data.Length;

            await ctx.Response.OutputStream.WriteAsync(
                data,
                0,
                data.Length);

            ctx.Response.OutputStream.Close();
            ctx.Response.Close();

            cache.PrintCache();
        }
        catch (Exception ex)
        {
            logger.Log(
                $"GREŠKA: {ex.Message}");

            try
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.Close();
            }
            catch
            {
            }
        }
    }
}
