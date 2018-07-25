using System;
using System.Reflection;
using System.Threading.Tasks;
using Contract;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using StructureMap;
using Pat.Sender;
using Pat.Sender.Correlation;
using Pat.Sender.Log4Net;

namespace Publisher
{
    internal static class Program
    {
        private static async Task Main()
        {
            var serviceProvider = InitialiseIoC();

            var publisher = serviceProvider.GetInstance<IMessagePublisher>();

            await publisher.PublishEvent(new Foo());
        }

        private static IContainer  InitialiseIoC()
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

            var container = new Container(x =>
            {
                x.Scan(scanner =>
                {
                    scanner.WithDefaultConventions();
                    scanner.AssemblyContainingType<IMessagePublisher>();
                });

                x.For<ICorrelationIdProvider>().Use(new LiteralCorrelationIdProvider($"{Guid.NewGuid()}"));
                x.For<PatSenderSettings>().Use(sender);
                x.For<IPatSenderLog>().Use<PatSenderLog4NetAdapter>();
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
