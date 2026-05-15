# CanHub

[简体中文](README.zh-CN.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Vector and ZLG: Apache-2.0](https://img.shields.io/badge/Vector%20%2F%20ZLG-Apache--2.0-orange.svg)](LICENSE-APACHE-2.0)
[![NuGet](https://img.shields.io/nuget/v/CanHub.Core.svg)](https://www.nuget.org/packages/CanHub.Core)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

CanHub is a .NET 10 CAN/CAN FD device abstraction library. It gives application code one stable API for CAN hardware while keeping each vendor driver isolated in its own adapter package.

The current release surface includes the core frame model, registry and DI infrastructure, an in-process virtual adapter, Vector XL Driver support, and ZLG USBCANFD support. Samples and hardware probe utilities are intentionally kept out of the phase-one release surface.

## Packages

| Package | Purpose | License |
| --- | --- | --- |
| [CanHub.Abstractions](src/CanHub.Abstractions/README.md) | Frame model, bus contracts, leases, scan types, manifests | MIT |
| [CanHub.Core](src/CanHub.Core/README.md) | DI registration, adapter registry, endpoint parsing, broadcast hub, lease conflict helpers | MIT |
| [CanHub.Adapter.Virtual](src/CanHub.Adapter.Virtual/README.md) | In-process virtual CAN/CAN FD adapter for tests and local tools | MIT |
| [CanHub.Adapter.Vector](src/CanHub.Adapter.Vector/README.md) | Vector XL Driver adapter with shared-channel leasing | Apache-2.0 |
| [CanHub.Adapter.Zlg](src/CanHub.Adapter.Zlg/README.md) | ZLG USBCANFD adapter | Apache-2.0 |

Chinese package READMEs are available next to each English README.

## Install

Install `CanHub.Core` plus the adapters your application needs:

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Virtual
dotnet add package CanHub.Adapter.Vector
dotnet add package CanHub.Adapter.Zlg
```

Applications that only define contracts or exchange frames can reference `CanHub.Abstractions` directly.

## Quick Start

```csharp
using CanHub;
using CanHub.Adapter.Virtual;

var registry = CanHubRegistry.CreateDefault()
    .AddVirtualAdapter();

await using var bus = await registry.OpenAsync(
    "virtual://demo?channel=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);

var frame = CanFrame.CreateData(
    CanId.Standard(0x123),
    new byte[] { 0x11, 0x22, 0x33 });

var result = await bus.SendAsync(frame, ct: CancellationToken.None);
Console.WriteLine($"Submitted: {result.CorrelationId}");
```

## Endpoint Format

Adapters own their endpoint scheme and query parameters:

```text
virtual://demo?channel=0
vector://VN1630A?deviceIndex=0&channel=0
zlg://USBCANFD_200U?deviceIndex=0&channel=0
```

Use `CanHubRegistry.ScanAsync` to discover adapters that support scanning. Hardware adapters may return diagnostics when a driver runtime or device is missing.

## Build And Test

```bash
dotnet build CanHub.slnx -c Release
dotnet test CanHub.slnx -c Release
```

Hardware tests are opt-in so normal CI can run without CAN devices:

```powershell
$env:CANHUB_TEST_VECTOR = "1"
$env:CANHUB_TEST_ZLG = "1"
```

Vector ECU interaction tests require a separate explicit opt-in because they can interact with a real bench:

```powershell
$env:CANHUB_TEST_VECTOR_ECU = "1"
```

## Design Notes

- `CanHub.Abstractions` has no dependency on Core or vendor SDKs.
- Adapters are registered explicitly through DI or direct registry methods.
- `ICanBus.SendAsync` reports local submission. Bus-level outcomes are emitted later as `CanFrameEvent` values with matching correlation IDs.
- Sharing rules are adapter-owned. Core provides common registry and conflict-detection helpers, but native session ownership stays inside each adapter.

## License

CanHub.Abstractions, CanHub.Core, and CanHub.Adapter.Virtual are licensed under the MIT License.

CanHub.Adapter.Vector and CanHub.Adapter.Zlg are licensed under Apache License 2.0. Their native runtime files may also be subject to the corresponding vendor driver terms. See the adapter package READMEs and third-party notices for details.
