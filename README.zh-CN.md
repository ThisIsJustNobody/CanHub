# CanHub

[English](README.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Vector and ZLG: Apache-2.0](https://img.shields.io/badge/Vector%20%2F%20ZLG-Apache--2.0-orange.svg)](LICENSE-APACHE-2.0)
[![NuGet](https://img.shields.io/nuget/v/CanHub.Core.svg)](https://www.nuget.org/packages/CanHub.Core)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

CanHub 是一个面向 .NET 10 的 CAN/CAN FD 设备抽象库。它为应用层提供统一、稳定的 CAN 访问 API，同时把不同厂商的驱动细节隔离在独立适配器包中。

当前发布范围包含帧模型、注册表与 DI 基础设施、进程内虚拟适配器、Vector XL Driver 适配器，以及 ZLG USBCANFD 适配器。示例项目和硬件探测工具暂不纳入第一阶段发布范围。

## 包列表

| 包 | 用途 | 协议 |
| --- | --- | --- |
| [CanHub.Abstractions](src/CanHub.Abstractions/README.zh-CN.md) | 帧模型、总线契约、租约、扫描类型、适配器清单 | MIT |
| [CanHub.Core](src/CanHub.Core/README.zh-CN.md) | DI 注册、适配器注册表、端点解析、广播中心、租约冲突辅助 | MIT |
| [CanHub.Adapter.Virtual](src/CanHub.Adapter.Virtual/README.zh-CN.md) | 用于测试和本地工具的进程内虚拟 CAN/CAN FD 适配器 | MIT |
| [CanHub.Adapter.Vector](src/CanHub.Adapter.Vector/README.zh-CN.md) | 基于 Vector XL Driver 的适配器，支持共享通道租约 | Apache-2.0 |
| [CanHub.Adapter.Zlg](src/CanHub.Adapter.Zlg/README.zh-CN.md) | ZLG USBCANFD 适配器 | Apache-2.0 |

每个包目录都同时提供英文和简体中文 README。

## 安装

应用通常安装 `CanHub.Core`，并按需安装具体适配器：

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Virtual
dotnet add package CanHub.Adapter.Vector
dotnet add package CanHub.Adapter.Zlg
```

如果项目只需要定义契约或交换帧数据，可以只引用 `CanHub.Abstractions`。

## 快速开始

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

## 端点格式

端点 scheme 和查询参数由各适配器定义：

```text
virtual://demo?channel=0
vector://VN1630A?deviceIndex=0&channel=0
zlg://USBCANFD_200U?deviceIndex=0&channel=0
```

支持扫描的适配器可以通过 `CanHubRegistry.ScanAsync` 发现设备。硬件适配器在驱动运行时或设备缺失时会尽量返回诊断信息。

## 构建和测试

```bash
dotnet build CanHub.slnx -c Release
dotnet test CanHub.slnx -c Release
```

硬件测试默认跳过，避免普通 CI 依赖真实 CAN 设备：

```powershell
$env:CANHUB_TEST_VECTOR = "1"
$env:CANHUB_TEST_ZLG = "1"
```

Vector ECU 交互测试需要额外显式开启，因为它可能访问真实台架：

```powershell
$env:CANHUB_TEST_VECTOR_ECU = "1"
```

## 设计要点

- `CanHub.Abstractions` 不依赖 Core 或任何厂商 SDK。
- 适配器通过 DI 或直接注册表方法显式注册，不做反射自动扫描。
- `ICanBus.SendAsync` 表示本地提交结果；总线级结果随后通过携带相同关联 ID 的 `CanFrameEvent` 上报。
- 共享规则由适配器负责。Core 提供注册表和冲突检测辅助，但原生会话所有权保留在适配器内部。

## 协议

CanHub.Abstractions、CanHub.Core 和 CanHub.Adapter.Virtual 使用 MIT License。

CanHub.Adapter.Vector 和 CanHub.Adapter.Zlg 使用 Apache License 2.0。它们携带或调用的厂商原生运行时文件还可能受对应厂商驱动条款约束，详情见各适配器 README 和第三方声明。
