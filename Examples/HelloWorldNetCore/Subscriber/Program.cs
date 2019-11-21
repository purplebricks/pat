using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pat.Subscriber;
using Pat.Subscriber.NetCoreDependencyResolution;
using Pat.Subscriber.Telemetry.StatsD;

namespace Subscriber
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>()
                        .AddPatLite(hostContext.Configuration.GetSection("Pat").Get<SubscriberConfiguration>())
                        .AddLogging(b => b.AddConsole())
                        .AddSingleton<IStatisticsReporter, StatisticsReporter>()
                        .AddSingleton<StatisticsReporterConfiguration>()
                        .AddHandlersFromAssemblyContainingType<FooHandler>();
                });
    }
}