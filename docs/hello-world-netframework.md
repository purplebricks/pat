---
layout: page
homepage: false
title: Hello World - .Net Framework
---

tl;dr - There is a complete example here [Examples/HelloWorldNetFramework](https://github.com/purplebricks/pat/tree/master/Examples/HelloWorldNetFramework)

# Prerequisites

This walk through will be done using .Net Framework 4.7, if you'd rather and example using .NET Core take a look here [Hello World - .Net Core](hello-world-dotnetcore.html)

We will also need access to an azure subscription in which we are able to create service bus namespaces and
topics. If you're not sure how to create an azure service bus, take a look at [Create a Service Bus namespace
using the Azure portal](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-create-namespace-portal).


# Objective
Our Hello World sample will have two applications, one will be the publisher, one the subscriber. The two 
applications will have shared knowledge of an event via a third project.

# First Steps

Firstly create 3 projects, each targeting .NET Framework 4.7. The first should be a class library called 
`Contract`, the other two should be console applications called `Publisher` and `Subscriber`. Both 
`Publisher` and `Subscriber` should reference the `Contract` project.

Add a new class to the `Contract` project, call it `Foo`, for now this doesn't need any properties. For a 
real event we would have something a bit more meaningful. 

We need to start by installing the latest versions of `Pat.Subscriber` and 
`Pat.Subscriber.StructureMap4DependencyResolution`, once they are installed we can build our event handler.

To handle a message in a Pat subscriber a class must implement the `IHandleEvent<T>` interface. The interface 
has a single method with the signature `Task HandleAsync(Foo @event)`. The simplest implementation of an event 
handler for our `Foo` contract is:

```
public class FooHandler : IHandleEvent<Foo>
{
    public async Task HandleAsync(Foo @event)
    {
        await Task.CompletedTask;
    }
}
```

A handler is designed to require as little knowledge of the service bus infrastructure as is possible. The key
thing to know at this point is that if the `HandleAsync` method completes successfully then the message is 
marked as [Complete](https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicebus.messaging.brokeredmessage.completeasync), 
if it throws an exception then the message is simply dropped by Pat. The azure  service bus will hold that 
message until the peek lock expires and the message will be received again by Pat. This will repeat until
the max delivery count has exceeded, at which point the message is moved onto the dead letter queue.

To show that our handler is receiving messages let's add some logging. Pat has a dependency on .NET Core Logging (`Microsoft.Extensions.Logging`). Add a new constructor with a parameter `ILogger log` to our handler, assign that to 
an instance variable called `_log`. Now update our `HandleAsync` method to the following.

```
public Task HandleAsync(Foo @event)
{
    _log.LogInformation($"Handling: {@event}");
    return Task.CompletedTask;
}
```

## Configuring the Subscribers Dependency Resolution

Now that we have a fully functional handler in place, so  we need to hook up the infrastructure to support it.

Setting up a new subscriber without dependency resolution is entirely possible, but not recommended. Instead 
create a new method `private static ServiceProvider InitialiseIoC()` in this we need to create the dependency 
configuration for our subscriber. For this example we can use:

```
var subscriberConfiguration = new SubscriberConfiguration
{
    ConnectionStrings = new[]
    {
        // Use your own service bus connection string here
        "Endpoint=sb://namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOURKEY"
    },
    TopicName = "pat",
    SubscriberName = "PatExampleSubscriber",
    UseDevelopmentTopic = false
};
```

For our IoC Pat provides the a structure map registry with sensible defaults. The simplest setup available 
setup is:

```
var container = new Container(x =>
{
    x.Scan(scanner =>
    {
        scanner.WithDefaultConventions();
    });

    x.AddRegistry(new PatLiteRegistryBuilder(subscriberConfiguration).Build());
    
    x.For<IStatisticsReporter>().Use(new StatisticsReporter(new StatisticsReporterConfiguration()));
    x.For<IPatSenderLog>().Use<PatSenderLog4NetAdapter>();
});

return container;
```

The line for the `StatisticsReporter` is support for Pat's telemetry reporting and these defaults won't be 
required in the future. Details on the statistics reporter are provided in the [telemetry](telemetry.html) 
documentation.

The `PatLiteRegistryBuilder` helper method configures the container for the Pat subscriber and its dependencies, 
the `scanner.WithDefaultConventions();` tells StructureMap and therefore Pat where to find the handlers in our 
app. If we have handlers split across multiple projects we'll need to tell StructureMap about them. The 
[StructureMap documentation](http://structuremap.github.io/quickstart/) covers that in more detail.

The line for `IPatSenderLog` plugs in log4net as a logger, make sure to reference the `Pat.Sender.Log4Net` package and register your log4net `ILog` type as per below.

N.B. For an example that uses .NET Core Logging, see [Hello World - .Net Core](hello-world-dotnetcore.html).

This example uses log4net for Pat's internal logging.  To help visualize what's happening it's useful to add a console 
appender for log4Net. To do this add `x.For<ILog>().Use(context => LogManager.GetLogger(context.ParentType));` 
to our container setup. Create a new method `InitLogger` (below) and call the method from our `InitialiseIoC` before 
the setup of our service provider. We can now see what Pat is doing internally.

```
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
```

## Bringing it all together

First we need to convert our Main methods signature to `async Task Main()`, for this we’ll need to enable C# 
7.1. This can be done by adding `<LangVersion>7.1</LangVersion>` to the `<PropertyGroup>` section in our 
subscriber’s csproj file.

The main method needs to perform 2 actions:

  1. Initialise the dependency resolution
  2. Publish an event

Initialising the dependency resolution is a case of calling `InitialiseIoC`. This is done by adding the 
following to our main method.

```
var serviceProvider = InitialiseIoC();
```

Publishing a message is done by getting an instance of `IMessagePublisher` and calling its `PublishEvent` method.

```
var publisher = serviceProvider.GetInstance<IMessagePublisher>();
await publisher.PublishEvent(new Foo());
```

# Running Our Example

Start by running the subscriber, the first time this starts up it will ensure that the filter on the 
subscription which it is subscribing to matches the messages it can handle.

Our output should look something like this:

```
2018-05-25 15:35:33,489 [1] INFO  Building subscription 1 on service bus **************.servicebus.windows.net/...
2018-05-25 15:35:35,374 [3] INFO  Validating subscription 'PatExampleSubscriber' rules on topic 'pat'...
2018-05-25 15:35:35,402 [3] INFO  Creating rule 1_v_1_0_0 for subscriber PatExampleSubscriber
2018-05-25 15:35:35,609 [3] INFO  Deleting rule $Default for subscriber PatExampleSubscriber, as it has been superceded by a newer version
2018-05-25 15:35:35,813 [4] INFO  Adding on subscription client 1 to list of source subscriptions
2018-05-25 15:35:35,817 [4] INFO  Listening for messages...
```

Hitting Ctrl+C results in 

```
2018-05-25 15:35:50,549 [3] INFO  Subscriber Shutdown Requested
```

Followed by the subscriber shutting down, this may take up to 60 seconds.

We can now run the publisher, this will publish our event and exit with nothing displayed on the console.

Running the subscriber again results in:

```
2018-05-25 15:39:24,004 [1] INFO  Building subscription 1 on service bus **************.servicebus.windows.net/...
2018-05-25 15:39:25,541 [3] INFO  Validating subscription 'PatExampleSubscriber' rules on topic 'pat'...
2018-05-25 15:39:25,557 [3] INFO  Adding on subscription client 1 to list of source subscriptions
2018-05-25 15:39:25,561 [3] INFO  Listening for messages...
2018-05-25 15:39:26,320 [3] DEBUG Message collection processing 1 messages
2018-05-25 15:39:27,506 [3] INFO  Handling: Contract.Foo
2018-05-25 15:39:27,647 [4] INFO  PatExampleSubscriber Success Handling Message 75cd3dcf-18ed-4e35-8ddc-397a7eb483b5 correlation id `c3234d5a-7f39-4563-b03a-c00e6843ca10`: Contract.Foo, Contract
```

From this we can see that the subscriber has successfully received and processed our message. Yeay!

# Next Steps

Now we have a working publisher and subscriber. It's worth reading through the other topics on the [getting 
started](getting-started.html) guide to help you build out a production ready subscriber.

# Troubleshooting

Common issues and trouble shooting guides are over on the [troubleshooting](troubleshooting.html) page