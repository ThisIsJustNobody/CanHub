# CanHub.Core

[简体中文](README.zh-CN.md)

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
await using var bus = await registry.OpenAsync(
    "virtual://demo?channel=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);

using var subscription = bus.Subscribe(new CanSubscriptionOptions());
```

Endpoint schemes are mapped to registered adapters. Unknown schemes fail before any vendor runtime is touched.

## Scan Devices

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);
```

The registry asks every adapter that supports scanning and returns both discovered devices and diagnostics. Missing hardware or missing native drivers should be surfaced as diagnostics rather than crashing the host process.

## Broadcast And Conflict Helpers

`FrameBroadcastHub` fans out frame events to subscriptions with bounded queues. Slow subscribers can be dropped according to subscription settings without blocking the adapter receive loop.

`LeaseConflictDetector` creates deterministic fingerprints for bus configuration so adapters can detect incompatible shared-channel requests.

## License

This package is licensed under the MIT License.
