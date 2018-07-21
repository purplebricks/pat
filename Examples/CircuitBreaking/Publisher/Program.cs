using System;
using System.Threading.Tasks;
using Contract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pat.Sender;
using Pat.Sender.Correlation;
using Pat.Sender.MessageGeneration;
using Pat.Sender.NetCoreLog;

namespace Publisher
{
    internal static class Program
    {
        private static async Task Main()
        {
            var serviceProvider = InitialiseIoC();

            var publisher = serviceProvider.GetService<IMessagePublisher>();
            var sending = true;
            Console.CancelKeyPress += (o, e) => { sending = false; };

            while (sending)
            {
                await publisher.PublishEvent(new Foo());
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        private static ServiceProvider InitialiseIoC()
        {
            var connection = "Endpoint=sb://namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOURKEY";
            var topicName = "pat";

            var settings = new PatSenderSettings
            {
                TopicName = topicName,
                PrimaryConnection = connection,
                UseDevelopmentTopic = false
            };

            var serviceProvider = new ServiceCollection()
                .AddPatSender(settings)
                .AddNetCoreLogging()
                .BuildServiceProvider();

            return serviceProvider;
        }

        private static IServiceCollection AddPatSender(this IServiceCollection services, PatSenderSettings settings)
            => services
                .AddSingleton(settings)
                .AddTransient<IMessagePublisher, MessagePublisher>()
                .AddTransient<IMessageSender, MessageSender>()
                .AddTransient<IMessageGenerator, MessageGenerator>()
                .AddTransient(s => new MessageProperties(new LiteralCorrelationIdProvider($"{Guid.NewGuid()}")));

        private static IServiceCollection AddNetCoreLogging(this IServiceCollection services)
            => services
                .AddTransient<IPatSenderLog, PatSenderNetCoreLogAdapter>()
                .AddLogging(b => b.AddConsole())
                .AddSingleton(s => s.GetRequiredService<ILoggerFactory>().CreateLogger("Publisher"));
    }
}
