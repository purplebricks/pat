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
using Pat.Subscriber.BatchProcessing;
using Pat.Subscriber.MessageProcessing;
using Pat.Subscriber.NetCoreDependencyResolution;
using Pat.Subscriber.RateLimiterPolicy;
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
            var connection = "Endpoint=sb://***REMOVED***.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=***REMOVED***";
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
                .DefineMessagePipeline
                .With<DefaultMessageProcessingBehaviour>()
                .With<RateLimiterMessageProcessingBehaviour>()
                .With<InvokeHandlerBehaviour>()
                .DefineBatchPipeline
                .With<RateLimiterBatchProcessingBehaviour>()
                .With<DefaultBatchProcessingBehaviour>()
                .Build();

            var serviceProvider = new ServiceCollection()
                .AddPatLite(patLiteOptions)
                .AddTransient(s => LogManager.GetLogger(s.GetType()))
                .AddTransient<IStatisticsReporter, StatisticsReporter>()
                .AddSingleton(new StatisticsReporterConfiguration())
                .AddHandlersFromAssemblyContainingType<FooHandler>()
                .AddSingleton(new RateLimiterPolicyOptions(
                    new RateLimiterConfiguration
                    {
                        RateLimit = 32
                    }))
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

            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
        }
    }
}
