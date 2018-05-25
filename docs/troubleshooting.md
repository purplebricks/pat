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


# Pat.Subscriber.Tools

"Response status code does not indicate success: 409 (Conflict)."

This seems to be an intermittent issue with the with the azure api's. Try again later. This is tracked in more
depth in this issue: [Fails to Create New Topic](https://github.com/purplebricks/Pat.Subscriber.Tools/issues/2)

