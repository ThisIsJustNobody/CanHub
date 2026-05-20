# CanHub.Adapter.Vector

[简体中文](README.zh-CN.md)

`CanHub.Adapter.Vector` connects CanHub to Vector CAN/CAN FD interfaces through the Vector XL Driver API. It provides endpoint parsing, native runtime loading, shared-channel leasing, capability metadata, and hardware diagnostics.

## Install

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Vector
```

The package targets Windows and includes managed/native assets needed by the adapter. The Vector driver stack must still be installed according to Vector's documentation, and the device must be visible to the XL Driver runtime.

## Register

```csharp
using CanHub;
using CanHub.Adapter.Vector;

var registry = CanHubRegistry.CreateDefault()
    .AddVectorAdapter();
```

## Endpoint Format

```text
vector://{deviceName}?deviceIndex={index}&channelIndex={channelIndex}
```

Examples:

```text
vector://VN1630A?deviceIndex=0&channelIndex=0
vector://VN1640A?deviceIndex=0&channelIndex=1&appName=CanHub
```

The adapter accepts legacy `channel` as a compatibility alias for `channelIndex`. Device names and channel numbering follow the information exposed by the Vector XL Driver.

## Usage

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);

await using var bus = await registry.OpenAsync(
    "vector://VN1630A?deviceIndex=0&channelIndex=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);
```

Use `CanOpenOptions.BusParameters` for CAN FD when the target channel and hardware support CAN FD, for example `CanBusParameters.Fd500k2M`. Incompatible shared-channel settings are rejected through adapter-owned lease checks.

## Recovery

Vector recovery is opt-in through `CanOpenOptions.Recovery`. The default is `CanRecoveryOptions.Disabled`, which reports bus/native errors without closing or reopening the channel.

When enabled, the adapter reuses the original open configuration, stops the receive loop, closes the XL port, reopens it, and restarts receive processing:

```csharp
await using var bus = await registry.OpenAsync(
    "vector://VN5610A?deviceIndex=0&channelIndex=2",
    new CanOpenOptions
    {
        BusParameters = CanBusParameters.Classic500k,
        Recovery = CanRecoveryOptions.ReopenWithBackoff(
            triggers: CanRecoveryTrigger.BusOff |
                      CanRecoveryTrigger.ErrorPassive |
                      CanRecoveryTrigger.NativeReceiveFault |
                      CanRecoveryTrigger.NativeTransmitFault)
    },
    CancellationToken.None);
```

`ResetOnFault` performs one close/reopen attempt. `ReopenWithBackoff` retries up to the configured attempt limit. Vector chip-state events can trigger `BusOff` or `ErrorPassive`; error frames, receive failures, and transmit failures can trigger the native fault options.

## Hardware Tests

Hardware tests are skipped unless explicitly enabled:

```powershell
$env:CANHUB_TEST_VECTOR = "1"
$env:CANHUB_TEST_VECTOR_DEVICE = "VN5610A"
$env:CANHUB_TEST_VECTOR_DEVICE_INDEX = "0"
$env:CANHUB_TEST_VECTOR_CHANNEL_INDEX = "2"
dotnet test tests/CanHub.Adapter.Vector.Tests/CanHub.Adapter.Vector.Tests.csproj -c Release
```

ECU interaction tests require an additional opt-in:

```powershell
$env:CANHUB_TEST_VECTOR_ECU = "1"
```

Keep ECU tests disabled in unattended CI unless the runner is connected to a known-safe bench.

## Third-Party Notices

This package may include or load Vector runtime files. See `THIRD-PARTY-NOTICES.md` in the package for attribution and driver-term notes. Installing or using Vector drivers may require accepting Vector's own license terms.

## License

This package is licensed under the Apache License 2.0.
