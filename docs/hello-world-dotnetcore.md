---
layout: page
homepage: false
title: Hello World - .Net Core
---

tl;dr - There is a complete example here [Examples/HelloWorldNetCore](https://github.com/purplebricks/pat/tree/master/Examples/HelloWorldNetCore)

# Prerequisites

This walk through will be done using .NET Core 2.1, if you'd rather an example in the full .NET Framework 
take a look here [Hello World - .Net Framework](hello-world-netframework.html)

We will also need access to an azure subscription in which we are able to create service bus namespaces and
topics. If you're not sure how to create an azure service bus, take a look at [Create a Service Bus namespace
using the Azure portal](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-create-namespace-portal).

# Objective
Our Hello World sample will have two applications

1. The publisher - this will publish the events to the Topic
2. The subscriber. 

The two applications will have shared knowledge of an event via a third project.

# Setup the solution

We need to create a new solution and 3 projects.

The first should be a class library called 
`Contract`, the other two should be console applications called `Publisher` and `Subscriber`. Both 
`Publisher` and `Subscriber` should reference the `Contract` project.

You can do this in Visual Studio or follow the command line steps below.

## Configure Solution via CLI

First create the projects:
```
dotnet new classlib -o Contracts
dotnet new console -o Publisher
dotnet new console -o Subscriber
```

`Publisher` and `Subscriber` projects both need to add a reference to `Contracts`:
```
dotnet add Publisher/Publisher.csproj reference Contracts/Contracts.csproj
dotnet add Subscriber/Subscriber.csproj reference Contracts/Contracts.csproj
```

Next, create the solution and add references to the projects:
```
dotnet new sln -n HelloWorldNetCore
dotnet sln add **/*.csproj
```

Now open the solution in your IDE/Edititor of choice.

# Create the contract

Delete `Class1.cs` from the `Contract` project.

Add a new class to the `Contract` project, call it `Foo`, for now this doesn't need any properties. For a 
real event we would have something a bit more meaningful.

# Building the Subscriber

Now that we have the skeleton projects in place we need to build a subscriber. This is the more complex of the 
two applications we need to build. 

We need to install the Pat.Sender Nuget packages. Fortunately we just need to install the latest versions of 
`Pat.Subscriber.NetCoreDependencyResolution` which holds a reference to the latest `Pat.Subscriber`. 

We can do this by running following command from the `Subscriber` directory: 
```
dotnet add package Pat.Subscriber.NetCoreDependencyResolution
```

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

To show that our handler is receiving messages let's add some logging. Pat already has a dependency on .NET Core Logging so we don't need to install (`Microsoft.Extensions.Logging`). 

Add a new constructor with a parameter `ILogger<FooHandler> log` to our handler, assign that to an instance variable called `_log`. Now update our `HandleAsync` method to the following.

```
public Task HandleAsync(Foo @event)
{
    _log.LogInformation("Handling: {event}", @event);
    await Task.CompletedTask;
}
```

## Configuring the Subscribers Dependency Resolution

Now that we have a fully functional handler in place, we need to hook up the infrastructure to support it.

> Note: Setting up a new subscriber without dependency resolution is entirely possible, but not recommended. 

In `Program.cs` create a new method `private static ServiceProvider InitialiseIoC()` in this we need to create the dependency configuration for our subscriber. For this example we can use:

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
    .AddLogging(b => b.AddConsole())
    .AddTransient<IStatisticsReporter, StatisticsReporter>()
    .AddSingleton(new StatisticsReporterConfiguration())
    .AddHandlersFromAssemblyContainingType<FooHandler>()
    .BuildServiceProvider();

return serviceProvider;
```

You will need to add following nuget packages to resolve the logging dependencies:
```
dotnet add package Microsoft.Extensions.Logging.Console
```

The two lines for the `StatisticsReporter` are part of Pat's telemetry reporting and these defaults won't be 
required in the future. Details on the statistics reporter are provided in the [telemetry](telemetry.html) 
documentation.

The `.AddPatLite` helper method configures the service collection for the Pat subscriber and its dependencies, 
the `.AddHandlersFromAssemblyContainingType` tells Pat where to find the handlers in our app. If we have 
handlers split across multiple projects we'll need to call this method multiple times. 

## Configuring Logging

Pat uses .NET Core Logging for its internal logging, to help visualize what's happening it's useful to add a console provider.

In the above IoC setup, the `.AddLogging(b => b.AddConsole())` line is configuring .NET Core Logging to provide a Console log.

## Bringing it all together.

First we need to convert our Main method's signature to `async Task Main()`, for this we'll need to enable C# 
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
    var log = serviceProvider.GetService<ILogger<Program>>();
    log.LogInformation("Subscriber Shutdown Requested");
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

Next we need to create the topic.

## Creating the Subscription

Now that our subscriber is complete we need to create a subscription on our service bus. This can be done 
manually or via a tool. The [pat](pat-subscriber-tools.html) global tool does this for us. The tool requires
that minimum of .Net Core 2.1.300 is installed.

To install the Pat tooling run:

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

We need to start by installing the latest versions of
- [Pat.Sender.NetCoreLogAdapter](https://www.nuget.org/packages/Pat.Sender.NetCoreLog/)
- [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/)
- [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/)

Navigate to the Publisher project and run:
```
dotnet add package Pat.Sender.NetCoreLog
dotnet add package Microsoft.Extensions.Logging.Console
dotnet add package Microsoft.Extensions.DependencyInjection
```

> Note: `Pat.Sender.NetCoreLogAdapter` brings in the dependency
 [Pat.Sender](https://www.nuget.org/packages/Pat.Sender/) meaning we don't have to explicitly add this package.

## Configuring the Publishers Dependency Resolution

> Note: Setting up a new publisher without dependency resolution is entirely possible, but not recommended.

In `program.cs` create a 
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
documentation pages. The simplest setup available is below:

```
    var serviceProvider = new ServiceCollection()
        .AddLogging(b => b.AddConsole())
        .AddPatSender(settings)
        .BuildServiceProvider();

    return serviceProvider;
```
Then add helper function:
```
private static IServiceCollection AddPatSender(this IServiceCollection services, PatSenderSettings settings)
            => services
                .AddPatSenderNetCoreLogAdapter()
                .AddSingleton(settings)
                .AddTransient<IMessagePublisher, MessagePublisher>()
                .AddTransient<IMessageSender, MessageSender>()
                .AddTransient<IMessageGenerator, MessageGenerator>()
                .AddTransient(s => new MessageProperties(new LiteralCorrelationIdProvider($"{Guid.NewGuid()}")));
```

### Using .NET Core Logging in the Publisher

Because Pat.Sender supports multiple logging frameworks, we use the NetCore Log Adapter (as shown above).

For more information on logging in dotnet core see [https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1).

> See our [Hello World - Full Framework example](!hello-world-netframework) for example using Log4Net


## Bringing it all together

First we need to convert our Main method's signature to `static async Task Main()`, for this we’ll need to enable C# 
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
info: Pat.Subscriber.SubscriptionBuilder[0]
      Building subscription 1 on service bus namespace.servicebus.windows.net/...
info: Pat.Subscriber.SubscriptionBuilder[0]
      Validating subscription 'PatExampleSubscriber' rules on topic 'pat'...
info: Pat.Subscriber.SubscriberRules.RuleApplier[0]
      Creating rule 1_v_1_0_0 for subscriber PatExampleSubscriber
info: Pat.Subscriber.SubscriberRules.RuleApplier[0]
      Deleting rule $Default for subscriber PatExampleSubscriber, as it has been superceded by a newer version
info: Pat.Subscriber.AzureServiceBusMessageReceiverFactory[0]
      Adding on subscription client 1 to list of source subscriptions
info: Pat.Subscriber.Subscriber[0]
      Listening for messages...
```

Hitting Ctrl+C results in 

```
info: Subscriber.Program[0]
      Subscriber Shutdown Requested
```

Followed by the subscriber shutting down, this may take up to 60 seconds.

We can now run the publisher, this will publish our event and exit with nothing displayed on the console.

Running the subscriber again results in:

```
info: Pat.Subscriber.SubscriptionBuilder[0]
      Building subscription 1 on service bus mailmachinegun-ns.servicebus.windows.net/...
info: Pat.Subscriber.SubscriptionBuilder[0]
      Validating subscription 'PatExampleSubscriber' rules on topic 'patDESKTOP-N2B8ALM'...
info: Pat.Subscriber.AzureServiceBusMessageReceiverFactory[0]
      Adding on subscription client 1 to list of source subscriptions
info: Pat.Subscriber.Subscriber[0]
      Listening for messages...
info: Subscriber.FooHandler[0]
      Handling: Contracts.Foo
```

From this we can see that the subscriber has successfully received and processed our message. Yeay!

# Next Steps

Now we have a working publisher and subscriber. It's worth reading through the other topics on the [getting 
started](getting-started.html) guide to help you build out a production ready subscriber.

# Troubleshooting

Common issues and trouble shooting guides are over on the [troubleshooting](troubleshooting.html) page