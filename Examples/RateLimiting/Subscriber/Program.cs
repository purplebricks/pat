using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                var log = serviceProvider.GetService<ILogger>();
                log.LogInformation("Subscriber Shutdown Requested");
                args.Cancel = true;
                tokenSource.Cancel();
            };

            var subscriber = serviceProvider.GetService<Pat.Subscriber.Subscriber>();
            await subscriber.Initialise(new[] { Assembly.GetExecutingAssembly() });
            await subscriber.ListenForMessages(tokenSource);
        }

        private static ServiceProvider InitialiseIoC()
        {
            var connection = "Endpoint=sb://namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOURKEY";
            var topicName = "pat";

            var subscriberConfiguration = new SubscriberConfiguration
            {
                ConnectionStrings = new[] { connection },
                TopicName = topicName,
                SubscriberName = "PatExampleSubscriber",
                UseDevelopmentTopic = false
            };

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
                .AddDefaultPatLogger()
                .AddLogging(b => b.AddConsole())
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
    }
}
