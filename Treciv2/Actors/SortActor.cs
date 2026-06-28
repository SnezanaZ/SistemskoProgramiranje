using System;
using System.Linq;
using Akka.Actor;

namespace Treciv2
{
    public class SortActor : ReceiveActor
    {
        public SortActor()
        {
            Receive<AggregatedData>(data =>
            {
                var sorted = data.Restaurants
                    .OrderByDescending(r => r.PriceLevel)
                    .ThenByDescending(r => r.Rating)
                    .ToList();

                Sender.Tell(new SortedData(sorted));

                Context.Stop(Self);
            });
        }
    }
}