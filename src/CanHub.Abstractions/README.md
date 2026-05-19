# CanHub.Abstractions

[简体中文](README.zh-CN.md)

`CanHub.Abstractions` contains the dependency-free contracts shared by every CanHub package. Reference it when a library needs to exchange CAN/CAN FD frames, describe device capabilities, or define adapter-facing interfaces without depending on DI, Core, or vendor runtimes.

## Install

```bash
dotnet add package CanHub.Abstractions
```

Target framework: `net10.0`.

## What Is Included

| Area | Main Types |
| --- | --- |
| Frame model | `CanFrame`, `CanFrameEvent`, `CanId`, `CanFrameFlags` |
| Bus contracts | `ICanBus`, `ICanSubscription`, `CanTransmitOptions`, `CanTransmitSubmissionResult` |
| Adapter contracts | `ICanAdapterProvider`, `CanAdapterManifest`, `CanCapability` |
| Lease contracts | `IDeviceLease`, `IChannelLease`, `ExclusivityModel` |
| Discovery | `CanChannelInfo`, `CanChannelScanResult`, `ScanOptions`, `ScanDiagnostic` |
| Status and errors | `CanStatusEvent`, `CanStatusKind`, `CanStatusCode`, `CanStatusSeverity`, `CanException`, `CanErrorCategory` |
| Recovery policy | `CanRecoveryOptions`, `CanRecoveryMode`, `CanRecoveryTrigger` |

## Frame Model

`CanFrame` is a readonly struct with inline payload storage for classic CAN and CAN FD frames. It avoids heap allocation for normal frame creation and exposes payload data through span-based APIs.

```csharp
using CanHub;

var classic = CanFrame.CreateData(
    CanId.Standard(0x123),
    new byte[] { 0x01, 0x02, 0x03 });

var fd = CanFrame.CreateFdData(
    CanId.Extended(0x18DAF110),
    stackalloc byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90 },
    bitrateSwitch: true);

Span<byte> buffer = stackalloc byte[64];
var length = fd.CopyPayloadTo(buffer);
```

Use `CopyPayloadTo(Span<byte>)` or `TryCopyPayloadTo` rather than storing mutable payload arrays.

## Transmission Contract

`ICanBus.SendAsync` reports local submission. It does not promise that another node accepted the frame. Bus-level outcomes, receive frames, and faults are emitted as `CanFrameEvent` values, usually with the same `CorrelationId` when the adapter can map the event back to a submission.

## Recovery Policy

Automatic bus recovery is opt-in. `CanOpenOptions.Recovery` defaults to `CanRecoveryOptions.Disabled`, so adapters report faults through `StatusChanged` without closing or reopening the channel unless the caller selects a policy. `ResetOnFault` means one immediate close/reopen using the original open context; `ReopenWithBackoff` uses the same close/reopen mechanism with delay and attempt limits.

## Adapter Manifests

Each adapter exposes a `CanAdapterManifest` so tools can inspect platform support, endpoint schemes, scan support, capabilities, and exclusivity behavior without loading vendor-specific details.

## License

This package is licensed under the MIT License.
