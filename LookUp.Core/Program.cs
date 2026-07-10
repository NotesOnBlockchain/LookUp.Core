using LookUp.Scanner.Helpers;

namespace LookUp.Core
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                using var host = CreateHostBuilder(args).Build();
                await host.RunWithTasksAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder => webBuilder
                .UseStartup<Startup>());
    }
}
