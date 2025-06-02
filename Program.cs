using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace avitoTelegramBot
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService() // 🚨 ОБЯЗАТЕЛЬНО: Windows Service
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<BotService>();
                });
    }
}