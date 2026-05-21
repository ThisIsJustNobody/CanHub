# CanHub.Adapter.Zlg

[English](README.md)

[![NuGet](https://img.shields.io/nuget/v/CanHub.Adapter.Zlg.svg)](https://www.nuget.org/packages/CanHub.Adapter.Zlg)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache--2.0-orange.svg)](https://github.com/ThisIsJustNobody/CanHub/blob/main/LICENSE-APACHE-2.0)

`CanHub.Adapter.Zlg` 通过 ZLG CAN 运行时将 CanHub 连接到 ZLG USBCANFD 设备。它提供端点解析、原生资产加载、总线生命周期管理、CAN/CAN FD 发送接收和硬件诊断。

## 安装

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Zlg
```

本包面向 Windows，并为支持的运行时标识包含适配器原生资产。ZLG 设备驱动仍需按照 ZLG 官方文档安装。

### 原生运行时布局

消费本包后，ZLG 运行时会复制到应用输出目录下：

```text
canhub/zlg/x64/zlgcan.dll
canhub/zlg/x64/kerneldlls/...
canhub/zlg/x86/zlgcan.dll
canhub/zlg/x86/kerneldlls/...
```

包消费构建会按进程/运行时架构只复制 `x64` 或 `x86`。包内 payload 放在 `buildTransitive/native`，不放在 NuGet `runtimes/*/native`，避免 RID 构建同时把 `zlgcan.dll` 放到输出根目录。

`ZlgNativeLoader` 优先加载 `canhub/zlg/<arch>/zlgcan.dll`，并通过 `AddDllDirectory` 注册该目录和 `kerneldlls` 子目录；不会修改 `PATH`。如需手工替换 ZLG 运行时，请替换对应 `canhub/zlg/<arch>` 目录里的文件，并保持 `zlgcan.dll` 与同级 `kerneldlls` 的目录结构。

## 注册

```csharp
using CanHub;
using CanHub.Adapter.Zlg;

var registry = CanHubRegistry.CreateDefault()
    .AddZlgAdapter();
```

## 端点格式

```text
zlg://{deviceType}?deviceIndex={index}&channelIndex={channelIndex}
```

示例：

```text
zlg://USBCANFD_200U?deviceIndex=0&channelIndex=0
```

按固定配置打开设备时，优先使用 `ZlgEndpoint`：

```csharp
CanEndpoint endpoint = ZlgEndpoint.UsbCanFd200U(deviceIndex: 0, channelIndex: 0);
```

首个支持的设备系列是 `USBCANFD_200U`。适配器接受旧 `channel` 参数作为 `channelIndex` 的兼容别名。设备索引和通道编号以 ZLG 运行时为准。若通道来自 `ScanAsync`，仍优先使用扫描结果里的 `CanChannelInfo.Endpoint` 或 `CanChannelInfo.CanonicalEndpoint`，不要重新手写拼接。

## 使用示例

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);

await using var bus = await registry.OpenAsync(
    ZlgEndpoint.UsbCanFd200U(deviceIndex: 0, channelIndex: 0),
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);
```

目标通道配置为 CAN FD 时，可通过 `CanOpenOptions.BusParameters` 传入 CAN FD 设置，例如 `CanBusParameters.Fd500k2M`。

## 诊断

打开通道前，可以用 `ZlgDiagnostics.CheckRuntimeAsync` 检查 Windows 平台、原生运行时文件、DLL 加载和设备扫描链路：

```csharp
var report = await ZlgDiagnostics.CheckRuntimeAsync(ct: CancellationToken.None);

if (!report.IsReady)
{
    foreach (var diagnostic in report.Diagnostics)
        Console.WriteLine($"{diagnostic.Category}: {diagnostic.Message} {diagnostic.Hint}");
}
```

该诊断会加载 ZLG 原生运行时并扫描设备，但不会打开具体通道。`report.HasOpenableChannel` 表示扫描结果中是否存在 CanHub 可尝试打开的通道。

## 自动恢复

ZLG 自动恢复通过 `CanOpenOptions.Recovery` 显式开启。默认值是 `CanRecoveryOptions.Disabled`，即只上报总线/原生错误，不主动关闭或重开通道。

开启后，适配器会复用原始打开配置，执行通道级关闭/重开：

```csharp
await using var bus = await registry.OpenAsync(
    "zlg://USBCANFD_200U?deviceIndex=0&channelIndex=0",
    new CanOpenOptions
    {
        BusParameters = CanBusParameters.Classic500k,
        Recovery = ZlgRecoveryProfiles.BusFaultBackoff
    });
```

`ZlgRecoveryProfiles.Disabled` 是默认的不重开策略。`ZlgRecoveryProfiles.BusFaultBackoff` 覆盖常见 ZLG 总线、原生接收和原生发送故障，初始等待 500ms，最多尝试 3 次。`ZlgRecoveryProfiles.ConservativeBench` 等待更久、尝试次数更多，适合台架环境。需要自定义触发条件或延迟时，仍可直接使用 `CanRecoveryOptions.ResetOnFault` 或 `CanRecoveryOptions.ReopenWithBackoff`。

实测 `USBCANFD_200U` 在错误注入或快速关闭/重开后，`ZCAN_ResetCAN`/`ZCAN_CloseDevice` 返回后驱动内部状态仍可能短暂残留；直接运行测试可能遇到 `ZCAN_StartCAN Error(0)`，而调试模式或加入等待后不复现。因此 ZLG 适配器在自动恢复重开前保留 500ms 的原生关闭稳定窗口：调用方配置的 `RestartDelay` 大于 500ms 时按调用方配置执行，小于 500ms（包括 `TimeSpan.Zero`）时按 500ms 执行。同时，`ZCAN_StartCAN` 若返回 `Error(0)`，适配器会先 `ZCAN_ResetCAN`，再等待 500ms 后重试启动，最多 6 次；该重试覆盖初次打开和恢复重开。

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

Vector 互通硬件测试还需要 `CANHUB_TEST_VECTOR=1`。默认使用 `VN5610A` 设备索引 `0`、通道 `2`，并假设该 Vector 通道接在 Bus1；可通过 `CANHUB_TEST_VECTOR_DEVICE`、`CANHUB_TEST_VECTOR_DEVICE_INDEX` 和 `CANHUB_TEST_VECTOR_CHANNEL_INDEX` 覆盖。若要运行 Bus2 无终端 Vector 故障恢复用例，还必须额外设置 `CANHUB_TEST_VECTOR_BUS2=1`，并确保 Vector 通道实际接入 Bus2。

ZLG 打开顺序诊断用例需额外设置 `CANHUB_TEST_ZLG_OPEN_DIAGNOSTICS=1`。该用例只反复打开/关闭两台 ZLG 在 Bus1 上的通道，不发送报文；可用 `CANHUB_TEST_ZLG_OPEN_DIAG_ITERATIONS` 调整重复次数，用于排查两个设备通道同时打开是否互相影响。

若直接运行失败但调试模式不失败，可用 `CANHUB_TEST_ZLG_OPEN_DIAG_STEP_DELAY_MS` 增加操作间隔，模拟调试器带来的延迟。需要更细分时，`CANHUB_TEST_ZLG_OPEN_DIAG_INTER_OPEN_DELAY_MS` 控制打开第一个通道后再打开第二个通道前的等待，`CANHUB_TEST_ZLG_OPEN_DIAG_AFTER_CLOSE_DELAY_MS` 控制每次关闭通道后的等待。本轮硬件观察中，加入 `StartCAN` 重试前，错误注入后继续跑打开顺序诊断时，关闭后等待 500ms、2000ms 甚至 5000ms 都可能稳定复现 `deviceIndex=1&channelIndex=0` 的 `ZCAN_StartCAN Error(0)`；加入 `StartCAN` 重试后，无额外延迟和带 2000ms 关闭后等待的诊断均可通过。该诊断覆盖的是手动反复关闭/打开不同 ZLG 设备的驱动状态窗口，和自动恢复的同通道重开路径不是同一个通过标准。

也可以直接使用仓库内置的诊断设置文件运行，避免手动设置环境变量：

```powershell
dotnet test tests/CanHub.Adapter.Zlg.Tests/CanHub.Adapter.Zlg.Tests.csproj `
  --settings tests/zlg-open-diagnostics.runsettings `
  --filter "FullyQualifiedName~ZlgOpenDiagnosticsHardwareTests" `
  --logger "console;verbosity=detailed"
```

若要对照调试模式的慢节奏，可运行带 2000ms 关闭后等待的设置文件：

```powershell
dotnet test tests/CanHub.Adapter.Zlg.Tests/CanHub.Adapter.Zlg.Tests.csproj `
  --settings tests/zlg-open-diagnostics-paced.runsettings `
  --filter "FullyQualifiedName~ZlgOpenDiagnosticsHardwareTests" `
  --logger "console;verbosity=detailed"
```

## 第三方运行时

本包可能包含或加载适配器所需的 ZLG 运行时文件。安装或使用 ZLG 驱动可能需要接受 ZLG 自身许可条款。

## 协议

本包使用 Apache License 2.0。
