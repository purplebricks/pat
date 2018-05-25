using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
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
                var log = serviceProvider.GetInstance<ILog>();
                log.Info("Subscriber Shutdown Requested");
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

            InitLogger();

            var container = new Container(x =>
            {
                x.Scan(scanner =>
                {
                    scanner.WithDefaultConventions();
                });

                x.AddRegistry(new PatLiteRegistryBuilder(subscriberConfiguration).Build());
                
                x.For<IStatisticsReporter>().Use(new StatisticsReporter(new StatisticsReporterConfiguration()));
                x.For<ILog>().Use(context => LogManager.GetLogger(context.ParentType));
            });

            return container;
        }

        private static void InitLogger()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository(Assembly.GetExecutingAssembly());
            var tracer = new TraceAppender();
            var patternLayout = new PatternLayout();

            patternLayout.ConversionPattern = "%d [%t] %-5p %m%n";
            patternLayout.ActivateOptions();

            tracer.Layout = patternLayout;
            tracer.ActivateOptions();
            hierarchy.Root.AddAppender(tracer);

            var appender = new ConsoleAppender();
            appender.Layout = patternLayout;
            appender.ActivateOptions();
            hierarchy.Root.AddAppender(appender);

            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
        }
    }
}
