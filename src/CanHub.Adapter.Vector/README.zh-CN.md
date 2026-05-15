# CanHub.Adapter.Vector

[English](README.md)

`CanHub.Adapter.Vector` 通过 Vector XL Driver API 将 CanHub 连接到 Vector CAN/CAN FD 设备。它提供端点解析、原生运行时加载、共享通道租约、能力元数据和硬件诊断。

## 安装

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Vector
```

本包面向 Windows，并包含适配器所需的托管/原生资产。Vector 驱动栈仍需按照 Vector 官方文档安装，设备也必须能被 XL Driver 运行时识别。

## 注册

```csharp
using CanHub;
using CanHub.Adapter.Vector;

var registry = CanHubRegistry.CreateDefault()
    .AddVectorAdapter();
```

## 端点格式

```text
vector://{deviceName}?deviceIndex={index}&channel={channelIndex}
```

示例：

```text
vector://VN1630A?deviceIndex=0&channel=0
vector://VN1640A?deviceIndex=0&channel=1&appName=CanHub
```

适配器也接受 `channelIndex` 作为 `channel` 的别名。设备名称和通道编号以 Vector XL Driver 暴露的信息为准。

## 使用示例

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);

await using var bus = await registry.OpenAsync(
    "vector://VN1630A?deviceIndex=0&channel=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);
```

目标通道和硬件支持 CAN FD 时，可通过 `CanOpenOptions.BusParameters` 配置 CAN FD 参数，例如 `CanBusParameters.Fd500k2M`。不兼容的共享通道设置会由适配器自己的租约检查拒绝。

## 硬件测试

硬件测试默认跳过，需显式开启：

```powershell
$env:CANHUB_TEST_VECTOR = "1"
dotnet test tests/CanHub.Adapter.Vector.Tests/CanHub.Adapter.Vector.Tests.csproj -c Release
```

ECU 交互测试需要额外开启：

```powershell
$env:CANHUB_TEST_VECTOR_ECU = "1"
```

除非 CI runner 连接到已知安全的台架，否则不要在无人值守 CI 中开启 ECU 测试。

## 第三方声明

本包可能包含或加载 Vector 运行时文件。归属信息和驱动条款说明见包内 `THIRD-PARTY-NOTICES.md`。安装或使用 Vector 驱动可能需要接受 Vector 自身许可条款。

## 协议

本包使用 Apache License 2.0。
