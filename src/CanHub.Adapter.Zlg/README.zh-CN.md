# CanHub.Adapter.Zlg

[English](README.md)

`CanHub.Adapter.Zlg` 通过 ZLG CAN 运行时将 CanHub 连接到 ZLG USBCANFD 设备。它提供端点解析、原生资产加载、总线生命周期管理、CAN/CAN FD 发送接收和硬件诊断。

## 安装

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Zlg
```

本包面向 Windows，并为支持的运行时标识包含适配器原生资产。ZLG 设备驱动仍需按照 ZLG 官方文档安装。

## 注册

```csharp
using CanHub;
using CanHub.Adapter.Zlg;

var registry = CanHubRegistry.CreateDefault()
    .AddZlgAdapter();
```

## 端点格式

```text
zlg://{deviceType}?deviceIndex={index}&channel={channelIndex}
```

示例：

```text
zlg://USBCANFD_200U?deviceIndex=0&channel=0
```

首个支持的设备系列是 `USBCANFD_200U`。设备索引和通道编号以 ZLG 运行时为准。

## 使用示例

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);

await using var bus = await registry.OpenAsync(
    "zlg://USBCANFD_200U?deviceIndex=0&channel=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);
```

目标通道配置为 CAN FD 时，可通过 `CanOpenOptions.BusParameters` 传入 CAN FD 设置，例如 `CanBusParameters.Fd500k2M`。

## 硬件测试

硬件测试默认跳过，需显式开启：

```powershell
$env:CANHUB_TEST_ZLG = "1"
dotnet test tests/CanHub.Adapter.Zlg.Tests/CanHub.Adapter.Zlg.Tests.csproj -c Release
```

普通 CI 中请保持该变量关闭，除非 runner 已连接支持的 ZLG 设备并安装了驱动。

## 第三方运行时

本包可能包含或加载适配器所需的 ZLG 运行时文件。安装或使用 ZLG 驱动可能需要接受 ZLG 自身许可条款。

## 协议

本包使用 Apache License 2.0。
