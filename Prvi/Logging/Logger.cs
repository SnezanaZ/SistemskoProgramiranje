public class Logger
{
    private readonly object lockObj = new();

    public void Log(string poruka)
    {
        lock(lockObj)
        {
            File.AppendAllText("log.txt",$"${DateTime.Now}: {poruka}\n");
        }
    }
}