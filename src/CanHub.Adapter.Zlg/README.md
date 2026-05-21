# CanHub.Adapter.Zlg

[简体中文](README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/CanHub.Adapter.Zlg.svg)](https://www.nuget.org/packages/CanHub.Adapter.Zlg)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache--2.0-orange.svg)](https://github.com/ThisIsJustNobody/CanHub/blob/main/LICENSE-APACHE-2.0)

`CanHub.Adapter.Zlg` connects CanHub to ZLG USBCANFD devices through the ZLG CAN runtime. It provides endpoint parsing, native asset loading, bus lifecycle management, CAN/CAN FD transmission, and hardware diagnostics.

## Install

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Zlg
```

The package targets Windows and includes adapter native assets for supported runtime identifiers. The ZLG device driver must be installed according to ZLG's documentation.

### Native Runtime Layout

When this package is consumed, the ZLG runtime is copied under the application output directory as:

```text
canhub/zlg/x64/zlgcan.dll
canhub/zlg/x64/kerneldlls/...
canhub/zlg/x86/zlgcan.dll
canhub/zlg/x86/kerneldlls/...
```

Package builds select the process/runtime architecture and copy only `x64` or `x86` for consumer projects. The package stores the payload under `buildTransitive/native` instead of NuGet `runtimes/*/native` so RID builds do not also place `zlgcan.dll` in the output root.

`ZlgNativeLoader` prefers `canhub/zlg/<arch>/zlgcan.dll`, registers that directory plus `kerneldlls` subdirectories with `AddDllDirectory`, and does not mutate `PATH`. To override the bundled runtime manually, replace the files in the corresponding `canhub/zlg/<arch>` directory while preserving the `zlgcan.dll` + sibling `kerneldlls` layout.

## Register

```csharp
using CanHub;
using CanHub.Adapter.Zlg;

var registry = CanHubRegistry.CreateDefault()
    .AddZlgAdapter();
```

## Endpoint Format

```text
zlg://{deviceType}?deviceIndex={index}&channelIndex={channelIndex}
```

Example:

```text
zlg://USBCANFD_200U?deviceIndex=0&channelIndex=0
```

Prefer `ZlgEndpoint` when opening a fixed device from configuration:

```csharp
CanEndpoint endpoint = ZlgEndpoint.UsbCanFd200U(deviceIndex: 0, channelIndex: 0);
```

The first supported device family is `USBCANFD_200U`. The adapter accepts legacy `channel` as a compatibility alias for `channelIndex`. Device index and channel numbering follow the ZLG runtime. If the channel came from `ScanAsync`, prefer the scanned `CanChannelInfo.Endpoint` or `CanChannelInfo.CanonicalEndpoint` instead of rebuilding it manually.

## Usage

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);

await using var bus = await registry.OpenAsync(
    ZlgEndpoint.UsbCanFd200U(deviceIndex: 0, channelIndex: 0),
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);
```

Use `CanOpenOptions.BusParameters` for CAN FD when the target channel is configured for CAN FD, for example `CanBusParameters.Fd500k2M`.

## Diagnostics

Use `ZlgDiagnostics.CheckRuntimeAsync` before opening a channel when you want to verify the Windows platform, native runtime files, DLL loading, and device scan path:

```csharp
var report = await ZlgDiagnostics.CheckRuntimeAsync(ct: CancellationToken.None);

if (!report.IsReady)
{
    foreach (var diagnostic in report.Diagnostics)
        Console.WriteLine($"{diagnostic.Category}: {diagnostic.Message} {diagnostic.Hint}");
}
```

The diagnostic check loads the ZLG native runtime and scans devices, but it does not open a specific channel. `report.HasOpenableChannel` indicates whether scanning found at least one channel that CanHub can try to open.

## Recovery

ZLG recovery is opt-in through `CanOpenOptions.Recovery`. The default is `CanRecoveryOptions.Disabled`, which reports bus/native errors without closing or reopening the channel.

When enabled, the adapter reuses the original open configuration and performs channel-level close/reopen recovery:

```csharp
await using var bus = await registry.OpenAsync(
    "zlg://USBCANFD_200U?deviceIndex=0&channelIndex=0",
    new CanOpenOptions
    {
        BusParameters = CanBusParameters.Classic500k,
        Recovery = ZlgRecoveryProfiles.BusFaultBackoff
    });
```

`ZlgRecoveryProfiles.Disabled` is the default no-reopen policy. `ZlgRecoveryProfiles.BusFaultBackoff` covers common ZLG bus/native receive/transmit faults with a 500ms initial delay and three attempts. `ZlgRecoveryProfiles.ConservativeBench` waits longer and allows more attempts for bench setups. You can still use `CanRecoveryOptions.ResetOnFault` or `CanRecoveryOptions.ReopenWithBackoff` directly when you need custom triggers or delays.

Observed on `USBCANFD_200U`: after error injection or fast close/reopen cycles, the native driver can keep transient state briefly after `ZCAN_ResetCAN`/`ZCAN_CloseDevice` has returned. Direct test runs may hit `ZCAN_StartCAN Error (0)`, while debugger-paced runs or added delay avoid it. The ZLG adapter therefore keeps a 500ms native-close settle window before automatic recovery reopen: caller-provided `RestartDelay` values above 500ms are honored, while smaller values, including `TimeSpan.Zero`, are raised to 500ms. If `ZCAN_StartCAN` returns `Error (0)`, the adapter resets the channel, waits 500ms, and retries the start up to six attempts; this covers both initial open and recovery reopen.

## Hardware Tests

Hardware tests are skipped unless explicitly enabled:

```powershell
$env:CANHUB_TEST_ZLG = "1"
$env:CANHUB_TEST_ZLG_DEVICE0 = "0"
$env:CANHUB_TEST_ZLG_DEVICE1 = "1"
$env:CANHUB_TEST_ZLG_BUS1_CHANNEL = "0"
$env:CANHUB_TEST_ZLG_BUS2_CHANNEL = "1"
dotnet test tests/CanHub.Adapter.Zlg.Tests/CanHub.Adapter.Zlg.Tests.csproj -c Release
```

Keep this variable disabled in normal CI unless the runner has a supported ZLG device and driver installed.

The recovery hardware tests assume two `USBCANFD_200U` devices and two buses: one terminated bus for normal traffic and single-node No ACK recovery, and one unterminated bus for stable bit/native bus error recovery.

Vector interop hardware tests additionally require `CANHUB_TEST_VECTOR=1`. By default they use `VN5610A` device index `0`, channel `2`, and assume that Vector channel is connected to Bus1; override with `CANHUB_TEST_VECTOR_DEVICE`, `CANHUB_TEST_VECTOR_DEVICE_INDEX`, and `CANHUB_TEST_VECTOR_CHANNEL_INDEX`. To run the Bus2 unterminated Vector recovery test, also set `CANHUB_TEST_VECTOR_BUS2=1` and connect the Vector channel to Bus2.

ZLG open-order diagnostics additionally require `CANHUB_TEST_ZLG_OPEN_DIAGNOSTICS=1`. This diagnostic only opens and closes the two ZLG devices on Bus1 without transmitting frames; set `CANHUB_TEST_ZLG_OPEN_DIAG_ITERATIONS` to change the repeat count when investigating whether simultaneously opened device channels affect each other.

When direct runs fail but debugger runs pass, use `CANHUB_TEST_ZLG_OPEN_DIAG_STEP_DELAY_MS` to add an operation delay that simulates debugger pacing. For finer control, `CANHUB_TEST_ZLG_OPEN_DIAG_INTER_OPEN_DELAY_MS` waits after opening the first channel and before opening the second channel, while `CANHUB_TEST_ZLG_OPEN_DIAG_AFTER_CLOSE_DELAY_MS` waits after each channel close. In this hardware round, before the `StartCAN` retry was added, open-order diagnostics after bus-error injection could still reproduce `ZCAN_StartCAN Error (0)` for `deviceIndex=1&channelIndex=0` with 500ms, 2000ms, and even 5000ms post-close delays. After adding the `StartCAN` retry, both the unpaced diagnostic and the 2000ms post-close paced diagnostic passed. This diagnostic covers repeated manual close/open cycles across different ZLG devices, so it is recorded separately from the automatic same-channel recovery path.

You can also use the checked-in diagnostics settings file instead of setting environment variables manually:

```powershell
dotnet test tests/CanHub.Adapter.Zlg.Tests/CanHub.Adapter.Zlg.Tests.csproj `
  --settings tests/zlg-open-diagnostics.runsettings `
  --filter "FullyQualifiedName~ZlgOpenDiagnosticsHardwareTests" `
  --logger "console;verbosity=detailed"
```

To compare against debugger-like pacing, run the settings file that waits 2000ms after each channel close:

```powershell
dotnet test tests/CanHub.Adapter.Zlg.Tests/CanHub.Adapter.Zlg.Tests.csproj `
  --settings tests/zlg-open-diagnostics-paced.runsettings `
  --filter "FullyQualifiedName~ZlgOpenDiagnosticsHardwareTests" `
  --logger "console;verbosity=detailed"
```

## Third-Party Runtime

This package may include or load ZLG runtime files needed by the adapter. Installing or using ZLG drivers may require accepting ZLG's own license terms.

## License

This package is licensed under the Apache License 2.0.
