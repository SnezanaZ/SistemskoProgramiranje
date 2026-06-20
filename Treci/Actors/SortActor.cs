using System.Linq;
using Akka.Actor;

namespace Treci
{
    public class SortActor : ReceiveActor
    {
        public SortActor()
        {
            Receive<AggregatedData>(data =>
            {
                var sorted =
                    data.Restaurants
                        .OrderByDescending(
                            r => r.PriceLevel)
                        .ToList();

                Sender.Tell(
                    new SortedData(sorted));
            });
        }
    }
}