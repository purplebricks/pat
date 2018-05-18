---
layout: page
homepage: false
sort: 2
title: Hello World - .Net Core
---

tl;dr - There is a complete example here [Examples/HelloWorldNetCore](https://github.com/purplebricks/pat/tree/master/Examples/HelloWorldNetCore)

# Prerequisites

This walk through will be done using .NET Core 2.0, if you'd rather and example in the full .NET Framework take a look here [Hello World - .Net Framework](hello-world-netframework.html)

You will also need access to an azure subscription in which you are able to create service bus namespaces and
topics. If you're not sure how to create an azure service bus take a look at [Create a Service Bus namespace
using the Azure portal](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-create-namespace-portal)

# Objective
Our Hello World sample will have two applications, one will be the publisher, one the subscriber. The two 
applications will have shared knowledge of an event via a third project.

# First Steps

Firstly create 3 projects, each should targeting .NET Core 2.0. The first should be a class library called 
`Contract`, the other two should be called console applications called `Publisher` and `Subscriber`. Both 
`Publisher` and `Subscriber` should reference the `Contract` project.

Add a new class to the `Contract` project, call it `Foo.cs`, for now this doesn't need any properties. For a
real event you can add in what you need. 

# Building the Subscriber

Now that we have the skeleton projects in place we need to build a subscriber. This is the more complex of 
the two projects we need to build. 

Start by installing the latest versions of `Pat.Subscriber` and `Pat.Subscriber.NetCoreDependencyResolution`,
once they are installed we can build our event handler.

To handle a message in Pat a class must implement the `IHandleEvent<T>` interface. The interface has a single method with the signature `Task HandleAsync(Foo @event)`. The simplest implementation of an event handler for our `Foo` contract is:
```
public class FooHandler : IHandleEvent<Foo>
{
    public async Task HandleAsync(Foo @event)
    {
        await Task.CompletedTask;
    }
}
```

A handler is designed to require as little knowledge of the service bus infrastructure as is possible. The 
key thing to know at this point is that if the `HandleAsync` method completes successfully then the message 
is marked as complete, if it throws an exception then the message is simply dropped by Pat. The azure 
service bus will hold that message until the peek lock and expired and the message will be received again
by Pat. This will repeat until the max delivery count has exceeded, at which point the message is moved 
onto the dead letter queue.

To show that our handler is receiving messages let's add some logging. Pat has a dependency on log4net, so for convenience let's use that. Add a new constructor with a parameter `ILog log` to your handler, assign that to a variable. Now update your `HandleAsync` method to the following.

```
public Task HandleAsync(Foo @event)
{
    _log.Info($"Handling: {@event}");
    return Task.CompletedTask;
}
```    

Now we have a fully functional handler in place we need to hook up the infrastructure to support it. Start by copy and pasting over the contents of [Program.cs](https://github.com/purplebricks/pat/blob/master/Examples/HelloWorldNetCore/Subscriber/Program.cs) into your
project.

The core of the code you've copied over is in the `InitialiseIoC` method. The only thing you _must_ change is
the value of the connection string. Copy this over from your service bus in the azure portal. 

# Building the Publisher

Explanation of the simplest possible pub / sub setup with Pat.

Once [pat#1](https://github.com/purplebricks/pat/pull/1) is resolved the sample will be hosted [here](https://github.com/purplebricks/pat/tree/master/Examples/FirstPubSub)

