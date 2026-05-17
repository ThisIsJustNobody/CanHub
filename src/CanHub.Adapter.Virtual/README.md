# CanHub.Adapter.Virtual

[简体中文](README.zh-CN.md)

`CanHub.Adapter.Virtual` provides an in-process CAN/CAN FD adapter for tests, demos, and local tooling. It requires no hardware or native driver and is useful for validating application logic before connecting real devices.

## Install

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Virtual
```

## Register

```csharp
using CanHub;
using CanHub.Adapter.Virtual;

var registry = CanHubRegistry.CreateDefault()
    .AddVirtualAdapter();
```

## Endpoint Format

```text
virtual://{busName}?channel={channelIndex}
```

Examples:

```text
virtual://demo?channel=0
virtual://demo?channel=1
virtual://isolated-test?channel=0
```

Endpoints with the same bus name share the same in-process virtual bus. Use different bus names to isolate tests. Bus names are case-sensitive.

## Usage

```csharp
await using var tx = await registry.OpenAsync(
    "virtual://demo?channel=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);

await using var rx = await registry.OpenAsync(
    "virtual://demo?channel=1",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);

using var subscription = rx.Subscribe(new CanSubscriptionOptions());

await tx.SendAsync(
    CanFrame.CreateData(CanId.Standard(0x123), new byte[] { 0x01, 0x02 }),
    ct: CancellationToken.None);
```

The adapter supports classic CAN and CAN FD frames. Scanning is intentionally not supported because virtual buses are created from endpoint names.

## Testing Notes

The virtual adapter is deterministic and hardware-free, so it is suitable for normal CI. It is also a good default for application tests that need to exercise subscription behavior, send correlation IDs, and basic multi-channel routing.

For recovery tests, pass a `VirtualRecoveryOptions` instance through `CanOpenOptions.NativeOptions` and keep its `VirtualFaultInjector`. Calling `InjectBusOff()` publishes a bus-off status and exercises the selected `CanOpenOptions.Recovery` policy without hardware.

```csharp
var injector = new VirtualFaultInjector();

await using var bus = await registry.OpenAsync(
    "virtual://recovery?channel=0",
    new CanOpenOptions
    {
        Recovery = CanRecoveryOptions.ResetOnFault(restartDelay: TimeSpan.Zero),
        NativeOptions = new VirtualRecoveryOptions { FaultInjector = injector }
    },
    CancellationToken.None);

injector.InjectBusOff();
```

## License

This package is licensed under the MIT License.
