using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                .AddDefaultPatLogger()
                .AddLogging(b => b.AddConsole())
                .AddTransient<IStatisticsReporter, StatisticsReporter>()
                .AddSingleton(new StatisticsReporterConfiguration())
                .AddHandlersFromAssemblyContainingType<FooHandler>()
                .BuildServiceProvider();

            return serviceProvider;
        }
    }
}
