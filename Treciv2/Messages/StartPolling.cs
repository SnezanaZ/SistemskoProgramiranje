namespace Treciv2
{
    public sealed class StartPolling
    {
        public StartPolling(string location)
        {
            Location = location;
        }

        public string Location { get; }
    }
}