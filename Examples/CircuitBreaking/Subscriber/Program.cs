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

            var patOptions = new PatLiteOptionsBuilder(subscriberConfiguration)
                .UseDefaultPipelinesWithCircuitBreaker(
                    sp =>
                    {
                        var monitoring = sp.GetRequiredService<CircuitBreakerMonitoring>();
                        return new CircuitBreakerBatchProcessingBehaviour.CircuitBreakerOptions(
                            circuitTestIntervalInSeconds: 30,
                            shouldCircuitBreak: IsCircuitBreakingException)
                        {
                            CircuitBroken = (sender, args) => monitoring.CircuitBroken(),
                            CircuitReset = (sender, args) => monitoring.CircuitReset(),
                            CircuitTest = (sender, args) => monitoring.CircuitTest()
                        };
                    })
                .Build();

            var serviceProvider = new ServiceCollection()
                .AddPatLite(patOptions)
                .AddDefaultPatLogger()
                .AddLogging(b => b.AddConsole())
                .AddTransient<IStatisticsReporter, StatisticsReporter>()
                .AddSingleton(new StatisticsReporterConfiguration())
                .AddHandlersFromAssemblyContainingType<FooHandler>()
                .AddSingleton<CircuitBreakerMonitoring>()
                .BuildServiceProvider();

            return serviceProvider;
        }

        private static bool IsCircuitBreakingException(Exception exception)
            => exception is ExternalProviderOfflineException;
    }
}
