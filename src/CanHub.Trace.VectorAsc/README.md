# CanHub.Trace.VectorAsc

[简体中文](README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/CanHub.Trace.VectorAsc.svg)](https://www.nuget.org/packages/CanHub.Trace.VectorAsc)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/ThisIsJustNobody/CanHub/blob/main/LICENSE)

`CanHub.Trace.VectorAsc` reads and writes Vector-style `.asc` CAN/CAN FD trace files for CanHub frame models.

The package targets `net10.0`, is hardware-independent, and depends only on `CanHub.Abstractions`.

## Install

```bash
dotnet add package CanHub.Trace.VectorAsc
```

## Read

```csharp
using CanHub.Trace.VectorAsc;

var trace = VectorAscReader.ReadText(File.ReadAllText("trace.asc"));

foreach (var record in trace.Frames)
{
    Console.WriteLine($"{record.Timestamp}: channel {record.ChannelIndex} {record.Direction} {record.Frame}");
}

foreach (var diagnostic in trace.Diagnostics)
{
    Console.WriteLine($"{diagnostic.LineNumber}: {diagnostic.Code} {diagnostic.Message}");
}
```

For large trace files, stream frames instead of materializing the full file:

```csharp
foreach (var record in VectorAscReader.ReadFileFrames("trace.asc"))
{
    Console.WriteLine($"{record.Timestamp}: channel {record.ChannelIndex} {record.Frame}");
}
```

## Write

```csharp
using CanHub;
using CanHub.Trace.VectorAsc;

var record = new VectorAscFrame
{
    Timestamp = TimeSpan.FromMilliseconds(1),
    ChannelIndex = 0,
    Direction = CanFrameDirection.Receive,
    ObservationKind = CanFrameObservationKind.Bus,
    Frame = CanFrame.CreateData(CanId.Standard(0x123), new byte[] { 0x01, 0x02, 0x03 })
};

var asc = VectorAscWriter.WriteText(new[] { record });
```

CAN FD output uses a CANoe-style conservative row shape: symbolic names are not emitted, DLC is written with lower-case hex in hex mode, CANoe-style high flags are emitted, and the trailing duration/flags/timing fields are retained.

## Supported Scope

- Header metadata: `date`, `base`, `timestamps`, internal events, trigger blocks.
- Classic CAN data, remote, and simple error frames.
- CAN FD data frames with BRS, ESI, DLC, data length, optional symbolic name, and trailing flags diagnostics.
- Standard and extended identifiers, including Vector's `x` suffix for extended IDs.

Unsupported rows such as LIN, signal values, bus statistics, chip status, and vendor-specific status events are skipped with diagnostics in tolerant mode. Strict mode throws `FormatException` for malformed or unsupported rows.

This package is intended for frame trace exchange. It does not claim to implement every row type accepted by CANoe/CANalyzer, and it does not perform database-backed signal decoding.

## License

This package is licensed under the MIT License.
