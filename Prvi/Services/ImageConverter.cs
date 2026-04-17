using System.Drawing;
using System.Drawing.Imaging;

public class ImageConverter
{
    public byte[] Convert(string path)
    {
        using var img = Image.FromFile(path);
        using var ms = new MemoryStream();
        img.Save(ms,ImageFormat.Png);
        return ms.ToArray();
    }
}