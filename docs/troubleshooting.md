---
layout: page
homepage: false
sort: 8
title: Troubleshooting
---

# Pat.Subscriber

"System.Reflection.TargetException: Non-static method requires a target."

This can happen in a when a .Net Core subscriber is missing an appropriate call to 
`.AddHandlersFromAssemblyContainingType` in its dependency resolution configuration.


"System.InvalidOperationException: subscriber <SubscriberName> does not have a filter for message type 
'<MessageType>'"

This happens when the subscriber receives a message for an event which does have a corresponding handler. This 
can happen if the subscriptions filter did not match the subscriber at startup. The filter is set at startup,
so no new additional message would be added to the subscription. However, pre-existing message stay in the 
subscription and need to be removed.

# Pat.Subscriber.Tools

"Response status code does not indicate success: 409 (Conflict)."

This seems to be an intermittent issue with the with the azure api's. Try again later. This is tracked in more
depth in this issue: [Fails to Create New Topic](https://github.com/purplebricks/Pat.Subscriber.Tools/issues/2)

