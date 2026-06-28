namespace Treciv2
{
    public sealed class GetCachedData
    {
        public GetCachedData(string location)
        {
            Location = location;
        }

        public string Location { get; }
    }
}