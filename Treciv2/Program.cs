using System.Threading.Tasks;
using Akka.Actor;
namespace Treciv2
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await YelpApp.Init();
        }
    }
}