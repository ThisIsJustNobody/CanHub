using CanHub.Adapter.Virtual.Internal;

namespace CanHub.Adapter.Virtual;

/// <summary>
/// 虚拟 CAN 适配器提供者。提供在内存中模拟 CAN 总线通信的能力，
/// 用于测试和开发场景。同一虚拟总线名称的通道之间可以互相通信。<br/>
/// Virtual CAN adapter provider. Provides in-memory CAN bus communication simulation
/// for testing and development scenarios. Channels sharing the same virtual bus name can communicate with each other.
/// </summary>
public sealed class VirtualAdapterProvider : ICanAdapterProvider
{
    /// <inheritdoc/>
    public string AdapterId => "virtual";

    /// <inheritdoc/>
    public string DisplayName => "Virtual CAN Bus";

    /// <inheritdoc/>
    public CanAdapterManifest Manifest { get; } = new(
        "virtual", "Virtual CAN Bus", new[] { "virtual" },
        platform: "cross-platform",
        exclusivity: ExclusivityModel.None,
        capabilities:
        [
            new CanCapability("classic-can", false, "Classic CAN support"),
            new CanCapability("can-fd", false, "CAN FD support"),
        ],
        supportsChannelScan: false);

    /// <summary>
    /// 打开一个虚拟 CAN 总线通道。根据 <paramref name="context"/> 中的 Endpoint
    /// 解析总线名称和通道索引，从 VirtualBusStore 获取或创建对应的 VirtualBusGroup
    /// 和 VirtualChannelState，然后构造 VirtualBusSession 实例。<br/>
    /// Opens a virtual CAN bus channel. Resolves the bus name and channel index from the
    /// <paramref name="context"/> Endpoint, acquires or creates the corresponding
    /// VirtualBusGroup and VirtualChannelState from VirtualBusStore, then constructs
    /// a VirtualBusSession instance.
    /// </summary>
    public ValueTask<ICanBus> OpenAsync(CanOpenContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var busParams = context.Options.BusParameters;
        var busName = context.Endpoint.Device;
        var channelIndex = context.Endpoint.ChannelIndex ?? 0;

        var (group, channelState) = VirtualBusStore.AcquireChannel(busName, channelIndex);
        channelState.ConfigureRecovery(context.Options.Recovery);

        if (context.Options.NativeOptions is VirtualRecoveryOptions virtualOptions &&
            virtualOptions.FaultInjector is not null)
        {
            channelState.RegisterFaultInjector(virtualOptions.FaultInjector);
        }

        var session = new VirtualBusSession(group, channelState, busParams.IsFd);
        return ValueTask.FromResult<ICanBus>(session);
    }

    /// <summary>
    /// 虚拟适配器不支持通道扫描操作，始终抛出 NotSupportedException。<br/>
    /// The virtual adapter does not support channel scanning and always throws NotSupportedException.
    /// </summary>
    public ValueTask<CanChannelScanResult> ScanAsync(
        ScanOptions? options = null, CancellationToken ct = default)
    {
        throw new NotSupportedException("Virtual adapter does not support channel scanning.");
    }
}
