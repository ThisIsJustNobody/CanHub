# CanHub.Adapter.Virtual

[English](README.md)

`CanHub.Adapter.Virtual` 提供进程内 CAN/CAN FD 虚拟适配器，适合测试、演示和本地工具。它不需要硬件或原生驱动，便于在接入真实设备前验证应用逻辑。

## 安装

```bash
dotnet add package CanHub.Core
dotnet add package CanHub.Adapter.Virtual
```

## 注册

```csharp
using CanHub;
using CanHub.Adapter.Virtual;

var registry = CanHubRegistry.CreateDefault()
    .AddVirtualAdapter();
```

## 端点格式

```text
virtual://{busName}?channel={channelIndex}
```

示例：

```text
virtual://demo?channel=0
virtual://demo?channel=1
virtual://isolated-test?channel=0
```

使用相同总线名的端点会共享同一个进程内虚拟总线。需要隔离测试时请使用不同总线名。总线名区分大小写。

## 使用示例

```csharp
await using var tx = await registry.OpenAsync(
    "virtual://demo?channel=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);

await using var rx = await registry.OpenAsync(
    "virtual://demo?channel=1",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);

using var subscription = rx.Subscribe(new CanSubscriptionOptions());

await tx.SendAsync(
    CanFrame.CreateData(CanId.Standard(0x123), new byte[] { 0x01, 0x02 }),
    ct: CancellationToken.None);
```

本适配器支持经典 CAN 和 CAN FD 帧。扫描功能有意不提供，因为虚拟总线由端点名称按需创建。

## 测试说明

虚拟适配器行为确定且不依赖硬件，适合普通 CI。应用测试如果需要覆盖订阅行为、发送关联 ID 或基础多通道路由，可以优先使用本适配器。

恢复相关测试可以通过 `CanOpenOptions.NativeOptions` 传入 `VirtualRecoveryOptions`，并保留其中的 `VirtualFaultInjector`。调用 `InjectBusOff()` 会发布 bus-off 状态，并在无硬件环境下触发当前 `CanOpenOptions.Recovery` 策略。

```csharp
var injector = new VirtualFaultInjector();

await using var bus = await registry.OpenAsync(
    "virtual://recovery?channel=0",
    new CanOpenOptions
    {
        Recovery = CanRecoveryOptions.ResetOnFault(restartDelay: TimeSpan.Zero),
        NativeOptions = new VirtualRecoveryOptions { FaultInjector = injector }
    },
    CancellationToken.None);

injector.InjectBusOff();
```

## 协议

本包使用 MIT License。
