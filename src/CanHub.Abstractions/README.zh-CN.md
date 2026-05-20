# CanHub.Abstractions

[English](README.md)

[![NuGet](https://img.shields.io/nuget/v/CanHub.Abstractions.svg)](https://www.nuget.org/packages/CanHub.Abstractions)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/ThisIsJustNobody/CanHub/blob/main/LICENSE)

`CanHub.Abstractions` 是所有 CanHub 包共享的零依赖契约层。库项目如果只需要交换 CAN/CAN FD 帧、描述设备能力，或定义适配器相关接口，可以只引用本包，而不依赖 DI、Core 或厂商运行时。

## 安装

```bash
dotnet add package CanHub.Abstractions
```

目标框架：`net10.0`。

## 包含内容

| 领域 | 主要类型 |
| --- | --- |
| 帧模型 | `CanFrame`, `CanFrameEvent`, `CanId`, `CanFrameFlags` |
| 总线契约 | `ICanBus`, `ICanSubscription`, `CanTransmitOptions`, `CanTransmitSubmissionResult` |
| 适配器契约 | `ICanAdapterProvider`, `CanAdapterManifest`, `CanCapability` |
| 租约契约 | `IDeviceLease`, `IChannelLease`, `ExclusivityModel` |
| 发现与扫描 | `CanChannelInfo`, `CanChannelScanResult`, `ScanOptions`, `ScanDiagnostic` |
| 状态和错误 | `CanStatusEvent`, `CanStatusKind`, `CanStatusCode`, `CanStatusSeverity`, `CanException`, `CanErrorCategory` |
| 恢复策略 | `CanRecoveryOptions`, `CanRecoveryMode`, `CanRecoveryTrigger` |

## 帧模型

`CanFrame` 是带内联载荷存储的只读结构，覆盖经典 CAN 和 CAN FD。常规创建路径不会产生堆分配，载荷通过基于 span 的 API 读取。

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

读取载荷时优先使用 `CopyPayloadTo(Span<byte>)` 或 `TryCopyPayloadTo`，避免在外部保存可变数组。

## 发送契约

`ICanBus.SendAsync` 表示本地提交结果，并不等同于远端节点已经接收。总线级发送结果、接收帧和故障会以 `CanFrameEvent` 形式上报；当适配器能够建立映射时，事件会携带与提交结果相同的 `CorrelationId`。

## 恢复策略

自动总线恢复必须显式开启。`CanOpenOptions.Recovery` 默认是 `CanRecoveryOptions.Disabled`，因此适配器只会通过 `StatusChanged` 上报故障，不会自动关闭或重开通道。`ResetOnFault` 表示使用原始 open context 立即执行一次 close/reopen；`ReopenWithBackoff` 使用同一套 close/reopen 机制，但带延迟和最大尝试次数。

## 适配器清单

每个适配器都会暴露 `CanAdapterManifest`，工具可以在不了解厂商细节的情况下读取平台支持、端点 scheme、扫描能力、功能能力和独占模型。

## 协议

本包使用 MIT License。
