namespace Treciv2
{
    public sealed class FetchRequest
    {
        public FetchRequest(string location)
        {
            Location = location;
        }

        public string Location { get; }
    }
}