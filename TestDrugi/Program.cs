public class Program
{
    public static async Task Main()
    {
        PerformanceTester tester =
            new PerformanceTester();

        await tester.RunAllTests();

        Console.WriteLine("Kraj testiranja.");
    }
}