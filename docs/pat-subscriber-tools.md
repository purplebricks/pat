---
layout: page
homepage: false
hide: true
sort: 1
title: Pat.Subscriber.Tools
---
[![Build status](https://ci.appveyor.com/api/projects/status/2l8rylk32jvd82xm?svg=true)](https://ci.appveyor.com/project/ilivewithian/pat-subscriber-tools)
[![NuGet](https://img.shields.io/nuget/v/Pat.Subscriber.Tools.svg)](https://www.nuget.org/packages/Pat.Subscriber.Tools/)

# Pat.Subscriber.Tools

This tool creates the subscriptions and topics required for a PatLite subscriber. The rules on the subscription are created by the subscriber at runtime.

## Installation

To install the dotnet tooling into a project add the following to a projects `.csproj` file:

```
<ItemGroup>
  <DotNetCliToolReference Include="Pat.Subscriber.Tools" Version="*" />
</ItemGroup>
```

## Usage

Navigate your terminal to the folder with the project you have installed the patlite tooling. Then run `dotnet restore`. At this point you are able to run `dotnet pat`. The pat tooling has a built in help which should help you run the tool.

### Example Use
To create a developer subscription on the pat topic you can run:
```
dotnet pat create -n mailmachinegun-ns -s MySubscription -t pat -d
```

To create a deployed  subscription on the pat topic you can run:
```
dotnet pat create -n mailmachinegun-ns -s MySubscription -t pat
```

If you need to run this from a non interactive environment you will need to provide details for a service principal. This can be done as follows:
```
dotnet pat create -n mailmachinegun-ns -s MySubscription -t pat -ci f44002f7-3843-453f-909e-efca3270ab6c -cs "correct horse battery staple" -ti 6965c8e5-c907-4361-905f-bba34a3b442a
```

Where `f44002f7-3843-453f-909e-efca3270ab6c` is the identifier for a service principal with appropriate permissions on your tenant with id `6965c8e5-c907-4361-905f-bba34a3b442a`

## Authentication
This tool requires authentication into your azure subscription. To make this flow more straightforward your authentication tokens are encrypted and stored in the file `%APPDATA%\PatLite\Tokencache.dat`. If you do not wish for your credentials to be stored you can either delete the file once the tool has run or you can run `dotnet pat logout`