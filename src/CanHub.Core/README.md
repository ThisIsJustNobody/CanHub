# CanHub.Core

[简体中文](README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/CanHub.Core.svg)](https://www.nuget.org/packages/CanHub.Core)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/ThisIsJustNobody/CanHub/blob/main/LICENSE)

`CanHub.Core` provides the coordination layer for CanHub applications: DI registration, adapter lookup, endpoint parsing, scanning fan-out, frame broadcasting, and configuration conflict helpers. It depends on `CanHub.Abstractions`, but not on vendor SDKs.

## Install

```bash
dotnet add package CanHub.Core
```

Add at least one adapter package, such as `CanHub.Adapter.Virtual`, `CanHub.Adapter.Vector`, or `CanHub.Adapter.Zlg`, before opening endpoints.

## Register Adapters With DI

Use DI when the host application already has a service container. Console applications that create their own provider may also need to reference `Microsoft.Extensions.DependencyInjection`.

```csharp
using CanHub;
using CanHub.Adapter.Virtual;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCanHub()
    .AddVirtualAdapter();

using var provider = services.BuildServiceProvider();
var registry = provider.GetRequiredService<CanHubRegistry>();
```

Adapters are registered explicitly. CanHub does not use reflection-based package auto-discovery.

For small tools and tests, a direct registry is also available:

```csharp
using CanHub;
using CanHub.Adapter.Virtual;

var registry = CanHubRegistry.CreateDefault()
    .AddVirtualAdapter();
```

## Open A Bus

```csharp
var endpoint = CanEndpoint.Create("virtual", "demo", channelIndex: 0);

await using var bus = await registry.OpenAsync(
    endpoint,
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);

using var subscription = bus.Subscribe(new CanSubscriptionOptions());
```

Endpoint schemes are mapped to registered adapters. Unknown schemes fail before any vendor runtime is touched. Adapter packages may provide dedicated endpoint builders, such as `ZlgEndpoint` or `VectorEndpoint`, for fixed-device configuration.

## Scan Devices

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);
```

The registry asks every adapter that supports scanning and returns both discovered devices and diagnostics. Missing hardware or missing native drivers should be surfaced as diagnostics rather than crashing the host process. If a channel came from scanning, prefer `registry.OpenAsync(channel, options, ct)` or the scanned endpoint fields instead of rebuilding the endpoint manually.

## Broadcast And Conflict Helpers

`FrameBroadcastHub` fans out frame events to subscriptions with bounded queues. Slow subscribers can be dropped according to subscription settings without blocking the adapter receive loop.

`LeaseConflictDetector` creates deterministic fingerprints for bus configuration so adapters can detect incompatible shared-channel requests.

## License

This package is licensed under the MIT License.
