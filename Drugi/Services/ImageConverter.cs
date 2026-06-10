using System.Drawing;
using System.Drawing.Imaging;

public class ImageConverter
{
    public byte[] Convert(string path)
    {
        using var fs = new FileStream(
    path,
    FileMode.Open,
    FileAccess.Read,
    FileShare.Read);

        using var img = Image.FromStream(fs);
        using var ms = new MemoryStream();
        img.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public Task<byte[]> ConvertAsync(string path)
    {
        return Task.Run(() => Convert(path));
    }
}