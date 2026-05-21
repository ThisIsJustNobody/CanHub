# CanHub.Adapter.Vector

[简体中文](README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/CanHub.Adapter.Vector.svg)](https://www.nuget.org/packages/CanHub.Adapter.Vector)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache--2.0-orange.svg)](https://github.com/ThisIsJustNobody/CanHub/blob/main/LICENSE-APACHE-2.0)

`CanHub.Adapter.Vector` connects CanHub to Vector CAN/CAN FD interfaces through the Vector XL Driver API. It provides endpoint parsing, native runtime loading, shared-channel leasing, capability metadata, and hardware diagnostics.

## Install

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Vector
```

The package targets Windows and includes managed/native assets needed by the adapter. The Vector driver stack must still be installed according to Vector's documentation, and the device must be visible to the XL Driver runtime.

Runtime layout:

```text
vxlapi_NET.dll
canhub/vector/x64/vxlapi64.dll
canhub/vector/x86/vxlapi.dll
```

The managed wrapper `vxlapi_NET.dll` remains in the application output root so advanced consumers can still reference `vxlapi_NET` directly. CanHub resolves the active-process native DLL from `canhub/vector/<arch>` and does not modify `PATH`. To manually replace the bundled Vector native runtime, replace the file in the matching architecture folder.

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
vector://VN1640A?deviceIndex=0&channelIndex=1
```

Prefer `VectorEndpoint` when opening a fixed device from configuration:

```csharp
CanEndpoint endpoint = VectorEndpoint.Create("VN1630A", deviceIndex: 0, channelIndex: 0);
```

The adapter accepts legacy `channel` as a compatibility alias for `channelIndex`. Device names and channel numbering follow the information exposed by the Vector XL Driver. If the channel came from `ScanAsync`, prefer the scanned `CanChannelInfo.Endpoint` or `CanChannelInfo.CanonicalEndpoint` instead of rebuilding it manually. Behavior options such as the Vector application name belong in `CanOpenOptions.NativeOptions`, not the endpoint.

## Usage

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);

await using var bus = await registry.OpenAsync(
    VectorEndpoint.Create("VN1630A", deviceIndex: 0, channelIndex: 0),
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
