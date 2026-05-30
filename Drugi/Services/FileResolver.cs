public class FileResolver
{
    private readonly string root;

    public FileResolver(string root)
    {
        this.root = root;
    }

    public string Resolve(string file)
    {
        return Path.Combine(root, file);
    }
}