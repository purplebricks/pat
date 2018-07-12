using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.DependencyInjection;
using Pat.Subscriber;
using Pat.Subscriber.CicuitBreaker;
using Pat.Subscriber.NetCoreDependencyResolution;
using Pat.Subscriber.Telemetry.StatsD;

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
                var log = serviceProvider.GetService<ILog>();
                log.Info("Subscriber Shutdown Requested");
                args.Cancel = true;
                tokenSource.Cancel();
            };

            var subscriber = serviceProvider.GetService<Pat.Subscriber.Subscriber>();
            await subscriber.Initialise(new[] {Assembly.GetExecutingAssembly()});
            await subscriber.ListenForMessages(tokenSource);
        }

        private static ServiceProvider InitialiseIoC()
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

            var patLiteOptions = new PatLiteOptionsBuilder(subscriberConfiguration)
                .UseDefaultPipelinesWithCircuitBreaker(s => s.GetService<CircuitBreakerBatchProcessingBehaviour.CircuitBreakerOptions>())
                .Build();

            var serviceProvider = new ServiceCollection()
                .AddSingleton(provider => new CircuitBreakerBatchProcessingBehaviour.CircuitBreakerOptions(30,
                    ex => ex is ExternalProviderOfflineException)
                {
                    CircuitBroken = (sender, args) =>
                    {
                        var monitoring = provider.GetService<CircuitBreakerMonitoring>();
                        monitoring.CircuitBroken();
                    },
                    CircuitReset = (sender, args) =>
                    {
                        var monitoring = provider.GetService<CircuitBreakerMonitoring>();
                        monitoring.CircuitReset();
                    },
                    CircuitTest = (sender, args) =>
                    {
                        var monitoring = provider.GetService<CircuitBreakerMonitoring>();
                        monitoring.CircuitTest();
                    }
                })                    
                .AddPatLite(patLiteOptions)
                .AddTransient(s => LogManager.GetLogger(s.GetType()))
                .AddTransient<IStatisticsReporter, StatisticsReporter>()
                .AddSingleton(new StatisticsReporterConfiguration())
                .AddHandlersFromAssemblyContainingType<FooHandler>()
                .BuildServiceProvider();

            return serviceProvider;
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

            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;
        }
    }
}
