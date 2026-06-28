namespace Treciv2
{
    public sealed class StopPolling
    {
        public StopPolling(string location)
        {
            Location = location;
        }

        public string Location { get; }
    }
}