using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pat.Subscriber;
using Pat.Subscriber.StructureMap4DependencyResolution;
using Pat.Subscriber.Telemetry.StatsD;
using StructureMap;

namespace Subscriber
{
    internal static class Program
    {
        private static async Task Main()
        {
            var serviceProvider = InitialiseIoC();

            var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, args) =>
            {
                var log = serviceProvider.GetInstance<ILogger>();
                log.LogInformation("Subscriber Shutdown Requested");
                args.Cancel = true;
                tokenSource.Cancel();
            };

            var subscriber = serviceProvider.GetInstance<Pat.Subscriber.Subscriber>();
            await subscriber.Initialise(new[] {Assembly.GetExecutingAssembly()});
            await subscriber.ListenForMessages(tokenSource);
        }

        private static IContainer InitialiseIoC()
        {
            var connection = "Endpoint=sb://namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOURKEY";
            var topicName = "pat";

            var subscriberConfiguration = new SubscriberConfiguration
            {
                ConnectionStrings = new[] {connection},
                TopicName = topicName,
                SubscriberName = "PatExampleSubscriber",
                UseDevelopmentTopic = false
            };

            var container = new Container(x =>
            {
                x.Scan(scanner =>
                {
                    scanner.WithDefaultConventions();
                });

                x.AddRegistry(new PatLiteRegistryBuilder(subscriberConfiguration)
                    .Build());
                
                x.For<IStatisticsReporter>().Use(new StatisticsReporter(new StatisticsReporterConfiguration()));
                x.For<ILoggerFactory>().Use(context => new LoggerFactory().AddConsole());
            });

            return container;
        }
    }
}
