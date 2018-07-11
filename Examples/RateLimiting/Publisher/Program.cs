using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Contract;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.DependencyInjection;
using Pat.Sender;
using Pat.Sender.Correlation;
using Pat.Sender.MessageGeneration;

namespace Publisher
{
    internal static class Program
    {
        private static async Task Main()
        {
            var serviceProvider = InitialiseIoC();

            var publisher = serviceProvider.GetService<IMessagePublisher>();

            await publisher.PublishEvents(Enumerable.Repeat(new Foo(), 200));
        }

        private static ServiceProvider InitialiseIoC()
        {
            var connection = "Endpoint=sb://namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOURKEY";
            var topicName = "pat";

            var sender = new PatSenderSettings
            {
                TopicName = topicName,
                PrimaryConnection = connection,
                UseDevelopmentTopic = false
            };

            InitLogger();

            var serviceProvider = new ServiceCollection()
                .AddSingleton(sender)
                .AddTransient<IMessagePublisher, MessagePublisher>()
                .AddTransient<IMessageSender, MessageSender>()
                .AddTransient(s => LogManager.GetLogger(s.GetType()))
                .AddTransient<IMessageGenerator, MessageGenerator>()
                .AddTransient(s => new MessageProperties(new LiteralCorrelationIdProvider($"{Guid.NewGuid()}")))
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
