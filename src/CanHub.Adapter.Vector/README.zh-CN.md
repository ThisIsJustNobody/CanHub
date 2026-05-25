# CanHub.Adapter.Vector

[English](README.md)

[![NuGet](https://img.shields.io/nuget/v/CanHub.Adapter.Vector.svg)](https://www.nuget.org/packages/CanHub.Adapter.Vector)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache--2.0-orange.svg)](https://github.com/ThisIsJustNobody/CanHub/blob/main/LICENSE-APACHE-2.0)

`CanHub.Adapter.Vector` 通过 Vector XL Driver API 将 CanHub 连接到 Vector CAN/CAN FD 设备。它提供端点解析、原生运行时加载、共享通道租约、能力元数据和硬件诊断。

## 安装

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Vector
```

本包面向 Windows，并包含适配器所需的托管/原生资产。Vector 驱动栈仍需按照 Vector 官方文档安装，设备也必须能被 XL Driver 运行时识别。

运行时目录布局：

```text
vxlapi_NET.dll
canhub/vector/x64/vxlapi64.dll
canhub/vector/x86/vxlapi.dll
```

托管 wrapper `vxlapi_NET.dll` 仍保留在应用输出根目录，方便高级用户直接引用 `vxlapi_NET`。CanHub 会从 `canhub/vector/<arch>` 解析当前进程架构对应的原生 DLL，且不会修改 `PATH`。如需人工替换随包携带的 Vector 原生运行时，请替换匹配架构目录中的文件。

## 注册

```csharp
using CanHub;
using CanHub.Adapter.Vector;

var registry = CanHubRegistry.CreateDefault()
    .AddVectorAdapter();
```

## 端点格式

```text
vector://{deviceName}?deviceIndex={index}&channelIndex={channelIndex}
```

示例：

```text
vector://VN1630A?deviceIndex=0&channelIndex=0
vector://VN1640A?deviceIndex=0&channelIndex=1
```

按固定配置打开设备时，优先使用 `VectorEndpoint`：

```csharp
CanEndpoint endpoint = VectorEndpoint.Create("VN1630A", deviceIndex: 0, channelIndex: 0);
```

适配器接受旧 `channel` 参数作为 `channelIndex` 的兼容别名。设备名称和通道编号以 Vector XL Driver 暴露的信息为准。若通道来自 `ScanAsync`，仍优先使用扫描结果里的 `CanChannelInfo.Endpoint` 或 `CanChannelInfo.CanonicalEndpoint`，不要重新手写拼接。Vector application name 等行为配置属于 `CanOpenOptions.NativeOptions`，不属于 endpoint。

## 使用示例

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);

await using var bus = await registry.OpenAsync(
    VectorEndpoint.Create("VN1630A", deviceIndex: 0, channelIndex: 0),
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);
```

目标通道和硬件支持 CAN FD 时，可通过 `CanOpenOptions.BusParameters` 配置 CAN FD 参数，例如 `CanBusParameters.Fd500k2M`。不兼容的共享通道设置会由适配器自己的租约检查拒绝。

Vector 通道可能已经被其他进程配置并激活。`VectorOpenOptions.IgnoreForeignConfiguration` 默认值为 `true`；当 XL Driver 对配置调用返回 `XL_ERR_INVALID_ACCESS` 时，适配器会通过 `StatusChanged` 上报 `ConfigurationIgnored` 警告，并继续激活通道用于收发。若当前进程必须确认参数由自己成功应用，请显式设置 `IgnoreForeignConfiguration = false`。CanHub 不会校验外部进程实际使用的位时序，调用方需要确认它与请求的 `CanBusParameters` 一致。

## 自动恢复

Vector 自动恢复通过 `CanOpenOptions.Recovery` 显式开启。默认值是 `CanRecoveryOptions.Disabled`，即只上报总线/原生错误，不主动关闭或重开通道。

开启后，适配器会复用原始打开配置，停止接收循环，关闭 XL 端口，重新打开端口，并恢复接收处理：

```csharp
await using var bus = await registry.OpenAsync(
    "vector://VN5610A?deviceIndex=0&channelIndex=2",
    new CanOpenOptions
    {
        BusParameters = CanBusParameters.Classic500k,
        Recovery = CanRecoveryOptions.ReopenWithBackoff(
            triggers: CanRecoveryTrigger.BusOff |
                      CanRecoveryTrigger.ErrorPassive |
                      CanRecoveryTrigger.NativeReceiveFault |
                      CanRecoveryTrigger.NativeTransmitFault)
    },
    CancellationToken.None);
```

`ResetOnFault` 只尝试一次关闭/重开；`ReopenWithBackoff` 会按配置的次数和退避策略重试。Vector chip-state 事件可触发 `BusOff` 或 `ErrorPassive`；错误帧、接收失败和发送失败可通过对应的原生故障触发项恢复。

## 硬件测试

硬件测试默认跳过，需显式开启：

```powershell
$env:CANHUB_TEST_VECTOR = "1"
$env:CANHUB_TEST_VECTOR_DEVICE = "VN5610A"
$env:CANHUB_TEST_VECTOR_DEVICE_INDEX = "0"
$env:CANHUB_TEST_VECTOR_CHANNEL_INDEX = "2"
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
