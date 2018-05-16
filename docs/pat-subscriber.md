---
layout: page
homepage: false
hide: true
sort: 1
title: Pat.Subscriber
---

# WIP - Project Documentation Still needs Work
# Pat.Subscriber

This is the subscriber, it's responsibility it is to listen for messages (events and command) on its subscription.

If you haven't already, please start with the [getting started](getting-started.html) guide before reading this.

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

