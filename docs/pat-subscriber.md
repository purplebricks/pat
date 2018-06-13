---
layout: page
homepage: false
hide: true
sort: 1
title: Pat.Subscriber
---

# WIP - Project Documentation Still needs Work
[![Build status](https://ci.appveyor.com/api/projects/status/nlrrpparg9658fx1?svg=true)](https://ci.appveyor.com/project/ilivewithian/pat-subscriber)
[![NuGet](https://img.shields.io/nuget/v/Pat.Subscriber.svg)](https://www.nuget.org/packages/Pat.Subscriber/)

# Pat.Subscriber

This is the subscriber, it's responsibility it is to listen for messages (events and command) on its subscription.

If you haven't already, please start with the [getting started](https://purplebricks.io/pat/docs/) guide before reading this.

## SubscriberConfiguration

TopicName = "pat";
UseDevelopmentTopic = true;
BatchSize = 16;
UsePartitioning = false;
ReceiveTimeoutSeconds = 60;

## BatchConfiguration

ReceiveTimeoutSeconds = receiveTimeoutSeconds;
BatchSize = batchSize;

## Message Serialisation

## Circuit Breaker

