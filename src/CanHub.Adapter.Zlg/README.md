# CanHub.Adapter.Zlg

[简体中文](README.zh-CN.md)

`CanHub.Adapter.Zlg` connects CanHub to ZLG USBCANFD devices through the ZLG CAN runtime. It provides endpoint parsing, native asset loading, bus lifecycle management, CAN/CAN FD transmission, and hardware diagnostics.

## Install

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Zlg
```

The package targets Windows and includes adapter native assets for supported runtime identifiers. The ZLG device driver must be installed according to ZLG's documentation.

## Register

```csharp
using CanHub;
using CanHub.Adapter.Zlg;

var registry = CanHubRegistry.CreateDefault()
    .AddZlgAdapter();
```

## Endpoint Format

```text
zlg://{deviceType}?deviceIndex={index}&channel={channelIndex}
```

Example:

```text
zlg://USBCANFD_200U?deviceIndex=0&channel=0
```

The first supported device family is `USBCANFD_200U`. Device index and channel numbering follow the ZLG runtime.

## Usage

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);

await using var bus = await registry.OpenAsync(
    "zlg://USBCANFD_200U?deviceIndex=0&channel=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);
```

Use `CanOpenOptions.BusParameters` for CAN FD when the target channel is configured for CAN FD, for example `CanBusParameters.Fd500k2M`.

## Hardware Tests

Hardware tests are skipped unless explicitly enabled:

```powershell
$env:CANHUB_TEST_ZLG = "1"
dotnet test tests/CanHub.Adapter.Zlg.Tests/CanHub.Adapter.Zlg.Tests.csproj -c Release
```

Keep this variable disabled in normal CI unless the runner has a supported ZLG device and driver installed.

## Third-Party Runtime

This package may include or load ZLG runtime files needed by the adapter. Installing or using ZLG drivers may require accepting ZLG's own license terms.

## License

This package is licensed under the Apache License 2.0.
