# CanHub.Trace.VectorAsc

[English](README.md)

`CanHub.Trace.VectorAsc` 用于读取和写入 Vector 风格的 `.asc` CAN/CAN FD Trace 文件，并映射到 CanHub 帧模型。

该包不依赖硬件或驱动，仅依赖 `CanHub.Abstractions`。

## 安装

```bash
dotnet add package CanHub.Trace.VectorAsc
```

## 读取

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

对于大型 Trace 文件，优先使用流式 API，避免一次性把全部帧载入内存：

```csharp
foreach (var record in VectorAscReader.ReadFileFrames("trace.asc"))
{
    Console.WriteLine($"{record.Timestamp}: channel {record.ChannelIndex} {record.Frame}");
}
```

## 写入

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

CAN FD 导出使用偏保守的 CANoe 风格行格式：不写出 symbolic name，hex 模式下 DLC 使用小写十六进制，写出 CANoe 风格高位 flags，并保留尾部 duration/flags/timing 字段。

## 支持范围

- 头部元数据：`date`、`base`、`timestamps`、internal events、trigger block。
- Classic CAN 数据帧、远程帧和简单错误帧。
- CAN FD 数据帧，支持 BRS、ESI、DLC、data length、可选 symbolic name，以及 trailing flags 一致性诊断。
- 标准帧和扩展帧 ID，包括 Vector 常见的扩展帧 `x` 后缀。

LIN、信号值、总线统计、芯片状态和厂商专用状态事件等不支持行，在宽松模式下会跳过并记录诊断。严格模式会对格式错误的受支持行抛出 `FormatException`。

## 许可证

本包使用 MIT License。
