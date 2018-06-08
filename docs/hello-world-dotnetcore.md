---
layout: page
homepage: false
sort: 2
title: Hello World - .Net Core
---

tl;dr - There is a complete example here [Examples/HelloWorldNetCore](https://github.com/purplebricks/pat/tree/master/Examples/HelloWorldNetCore)

# Prerequisites

This walk through will be done using .NET Core 2.0, if you'd rather an example in the full .NET Framework 
take a look here [Hello World - .Net Framework](hello-world-netframework.html)

We will also need access to an azure subscription in which we are able to create service bus namespaces and
topics. If you're not sure how to create an azure service bus, take a look at [Create a Service Bus namespace
using the Azure portal](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-create-namespace-portal).

# Objective
Our Hello World sample will have two applications, one will be the publisher, one the subscriber. The two 
applications will have shared knowledge of an event via a third project.

# First Steps

Firstly create 3 projects, each targeting .NET Core 2.0. The first should be a class library called 
`Contract`, the other two should be console applications called `Publisher` and `Subscriber`. Both 
`Publisher` and `Subscriber` should reference the `Contract` project.

Add a new class to the `Contract` project, call it `Foo`, for now this doesn't need any properties. For a 
real event we would have something a bit more meaningful. 

# Building the Subscriber

Now that we have the skeleton projects in place we need to build a subscriber. This is the more complex of the 
two applications we need to build. 

We need to start by installing the latest versions of `Pat.Subscriber` and 
`Pat.Subscriber.NetCoreDependencyResolution`, once they are installed we can build our event handler.

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

To show that our handler is receiving messages let's add some logging. Pat has a dependency on log4net, so for
convenience let's use that. Add a new constructor with a parameter `ILog log` to our handler, assign that to 
an instance variable called `_log`. Now update our `HandleAsync` method to the following.

```
public Task HandleAsync(Foo @event)
{
    _log.Info($"Handling: {@event}");
    return Task.CompletedTask;
}
```

## Configuring the Subscribers Dependency Resolution

Now that we have a fully functional handler in place, so  we need to hook up the infrastructure to support it.

Setting up a new subscriber without dependency resolution is entirely possible, but not recommended. Instead 
create a new method `private static ServiceProvider InitialiseIoC()` in this we need to create he dependency 
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

For our service collection Pat provides the dependency resolution helper `.AddPatLite` this sets some 
sensible defaults for us. The simplest setup available setup is:

```
var serviceProvider = new ServiceCollection()
    .AddPatLite(subscriberConfiguration)
    .AddTransient<IStatisticsReporter, StatisticsReporter>()
    .AddSingleton(new StatisticsReporterConfiguration())
    .AddHandlersFromAssemblyContainingType<FooHandler>()
    .BuildServiceProvider();

return serviceProvider;
```

The two lines for the `StatisticsReporter` are part of Pat's telemetry reporting and these defaults won't be 
required in the future. Details on the statistics reporter are provided in the [telemetry](telemetry.html) 
documentation.

The `.AddPatLite` helper method configures the service collection for the Pat subscriber and its dependencies, 
the `.AddHandlersFromAssemblyContainingType` tells pat where to find the handlers in our app. If we have 
handlers split across multiple projects we'll need to call this method multiple times. 

## Configuring Logging

Pat uses log4net for its internal logging, to help visualize what's happening it's useful to add a console 
appender for log4Net. To do this add `.AddTransient(s => LogManager.GetLogger(s.GetType()))` to our service 
collection setup. Create a new method `InitLogger` (below) and call the method from our `InitialiseIoC` 
before the setup of our service provider. We can now see what Pat is doing internally.

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

## Bringing it all together.

First we need to convert our Main methods signature to `async Task Main()`, for this we'll need to enable C# 
7.1. This can be done by adding `<LangVersion>7.1</LangVersion>` to the `<PropertyGroup>` section in our 
subscriber's csproj file.

The main method needs to perform 3 actions:

  1. Initialise the dependency resolution
  2. Provide a way to shut down Pat
  3. Start Pat.

Initialising the dependency resolution is done by calling `InitialiseIoC`. This is done by adding the 
following to our main method.

```
var serviceProvider = InitialiseIoC();
```

For a console app a nice way to enable shutdown is to listen for Ctrl+C and then gracefully shutdown. Since 
Pat might be processing a message we will trigger the cancellation of a continuation token and allow Pat to 
shutdown when it's ready. Pat can take up to a minute to shutdown, but if it's waiting on our handler this 
may take longer. This is done by adding the following to our main method:

```
var tokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, args) =>
{
    var log = serviceProvider.GetService<ILog>();
    log.Info("Subscriber Shutdown Requested");
    args.Cancel = true;
    tokenSource.Cancel();
};
```

Starting Pat is a case of creating an instance of the Pat Subscriber, initialising it and then listening for 
messages. This is done by adding the following to our main method:

```
var subscriber = serviceProvider.GetService<Pat.Subscriber.Subscriber>();
await subscriber.Initialise(new[] {Assembly.GetExecutingAssembly()});
await subscriber.ListenForMessages(tokenSource);
```

## Creating the Subscription

Now that our subscriber is complete we need to create a subscription on our service bus. This can be done 
manually or via a tool. The [pat](pat-subscriber-tools.html) global tool does this for us. The tool requires
that .Net Core 2.1.300 is installed.

To install the pat tooling run:

```
dotnet tool install -g Pat.Subscriber.Tools
```

Then in our prompt run (note some prompts need reopening before `pat` is available): 

```
pat create -n namespace -s PatExampleSubscriber -t pat
```

We need to replace `namespace` with the service bus namespace from our connection string.

# Building the Publisher

Publishing a message with Pat is somewhat simpler than subscribing to messages.

We need to start by installing the latest versions of `Pat.Publisher` and 
`Microsoft.Extensions.DependencyInjection`.

## Configuring the Publishers Dependency Resolution

Setting up a new publisher without dependency resolution is entirely possible, but not recommended. Create a 
new method `private static ServiceProvider InitialiseIoC()` in this we need to create the configuration for 
our publisher. For this example we can use:

```
var sender = new PatSenderSettings
{
    TopicName = "pat",
    // Use your own service bus connection string here
    PrimaryConnection = ""Endpoint=sb://namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOURKEY";",
    UseDevelopmentTopic = false
};
```

For our service collection Pat requires a few classes to be configured. The `IMessagePublisher` is the 
interface which we will use in our application. The concrete implementation of this requires a 
[MessageSender](pat-sender.html#message-sender), a [MessageGenerator](pat-sender.html#message-generator), 
[MessageProperties](pat-sender.html#message-properties) and [correlation 
ids](pat-sender.html#correlation-id-provider). The details of which are explained on their specific 
documentation pages. The simplest setup available is:

```
var serviceProvider = new ServiceCollection()
    .AddSingleton(sender)
    .AddTransient<IMessagePublisher, MessagePublisher>()
    .AddTransient<IMessageSender, MessageSender>()
    .AddTransient<IMessageGenerator, MessageGenerator>()
    .AddTransient(s => new MessageProperties(new LiteralCorrelationIdProvider($"{Guid.NewGuid()}")))
    .BuildServiceProvider();

return serviceProvider;
```

Pat uses log4net for its internal logging, to help visualize what's happening it's useful to add a console 
appender for log4Net. To do this add `.AddTransient(s => LogManager.GetLogger(s.GetType()))` to our service 
collection setup. Create a new method `InitLogger` (below) and call the method from our `InitialiseIoC` before 
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

## Brining it all together

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
var publisher = serviceProvider.GetService<IMessagePublisher>();
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
started](getting-started.html) guide to help you build out a production read subscriber.

# Troubleshooting

Common issues and trouble shooting guides are over on the [troubleshooting](troubleshooting.html) page