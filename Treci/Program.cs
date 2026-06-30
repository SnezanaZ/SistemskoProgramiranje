using System.Threading.Tasks;

namespace Treci
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new AppHost().RunAsync();
        }
    }
}