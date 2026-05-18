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

## 自动恢复

ZLG 自动恢复通过 `CanOpenOptions.Recovery` 显式开启。默认值是 `CanRecoveryOptions.Disabled`，即只上报总线/原生错误，不主动关闭或重开通道。

开启后，适配器会复用原始打开配置，执行通道级关闭/重开：

```csharp
await using var bus = await registry.OpenAsync(
    "zlg://USBCANFD_200U?deviceIndex=0&channel=0",
    new CanOpenOptions
    {
        BusParameters = CanBusParameters.Classic500k,
        Recovery = CanRecoveryOptions.ReopenWithBackoff(
            triggers: CanRecoveryTrigger.BusOff |
                      CanRecoveryTrigger.ErrorPassive |
                      CanRecoveryTrigger.NativeReceiveFault)
    });
```

`ResetOnFault` 只尝试一次关闭/重开；`ReopenWithBackoff` 会按配置的次数和退避策略重试。ZLG 的 ACK 错误、位错误等通用总线错误对象可通过 `CanRecoveryTrigger.NativeReceiveFault` 触发恢复。

## 硬件测试

硬件测试默认跳过，需显式开启：

```powershell
$env:CANHUB_TEST_ZLG = "1"
$env:CANHUB_TEST_ZLG_DEVICE0 = "0"
$env:CANHUB_TEST_ZLG_DEVICE1 = "1"
$env:CANHUB_TEST_ZLG_BUS1_CHANNEL = "0"
$env:CANHUB_TEST_ZLG_BUS2_CHANNEL = "1"
dotnet test tests/CanHub.Adapter.Zlg.Tests/CanHub.Adapter.Zlg.Tests.csproj -c Release
```

普通 CI 中请保持该变量关闭，除非 runner 已连接支持的 ZLG 设备并安装了驱动。

恢复相关硬件测试假设连接两台 `USBCANFD_200U` 和两条总线：一条带终端电阻，用于正常通信和单节点 No ACK 恢复；另一条不带终端电阻，用于稳定触发位错误/原生总线错误恢复。

Vector 互通硬件测试还需要 `CANHUB_TEST_VECTOR=1`。默认使用 `VN5610A` 设备索引 `0`、通道 `2`；可通过 `CANHUB_TEST_VECTOR_DEVICE`、`CANHUB_TEST_VECTOR_DEVICE_INDEX` 和 `CANHUB_TEST_VECTOR_CHANNEL_INDEX` 覆盖。

## 第三方运行时

本包可能包含或加载适配器所需的 ZLG 运行时文件。安装或使用 ZLG 驱动可能需要接受 ZLG 自身许可条款。

## 协议

本包使用 Apache License 2.0。
