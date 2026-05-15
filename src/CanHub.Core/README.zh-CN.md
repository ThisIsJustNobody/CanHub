# CanHub.Core

[English](README.md)

`CanHub.Core` 是 CanHub 应用的协调层，提供 DI 注册、适配器查找、端点解析、扫描聚合、帧广播和配置冲突辅助。它依赖 `CanHub.Abstractions`，但不依赖任何厂商 SDK。

## 安装

```bash
dotnet add package CanHub.Core
```

打开端点前还需要至少安装一个适配器包，例如 `CanHub.Adapter.Virtual`、`CanHub.Adapter.Vector` 或 `CanHub.Adapter.Zlg`。

## 使用 DI 注册适配器

宿主应用已经有服务容器时，推荐通过 DI 注册。普通控制台程序如果自己创建 provider，可能还需要引用 `Microsoft.Extensions.DependencyInjection`。

```csharp
using CanHub;
using CanHub.Adapter.Virtual;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCanHub()
    .AddVirtualAdapter();

using var provider = services.BuildServiceProvider();
var registry = provider.GetRequiredService<CanHubRegistry>();
```

适配器需要显式注册。CanHub 不使用基于反射的包自动发现。

小型工具和测试也可以直接创建注册表：

```csharp
using CanHub;
using CanHub.Adapter.Virtual;

var registry = CanHubRegistry.CreateDefault()
    .AddVirtualAdapter();
```

## 打开总线

```csharp
await using var bus = await registry.OpenAsync(
    "virtual://demo?channel=0",
    new CanOpenOptions { BusParameters = CanBusParameters.Classic500k },
    CancellationToken.None);

using var subscription = bus.Subscribe(new CanSubscriptionOptions());
```

注册表根据端点 scheme 选择适配器。未知 scheme 会在接触任何厂商运行时之前失败。

## 扫描设备

```csharp
var scan = await registry.ScanAsync(new ScanOptions(), CancellationToken.None);
```

注册表会调用所有支持扫描的适配器，并汇总设备和诊断信息。缺少硬件或原生驱动时，适配器应尽量返回诊断，而不是让宿主进程崩溃。

## 广播和冲突辅助

`FrameBroadcastHub` 使用有界队列把帧事件分发给订阅者。慢订阅者会按照订阅设置被丢弃或背压处理，避免阻塞适配器接收循环。

`LeaseConflictDetector` 为总线配置生成确定性指纹，帮助适配器识别不兼容的共享通道请求。

## 协议

本包使用 MIT License。
