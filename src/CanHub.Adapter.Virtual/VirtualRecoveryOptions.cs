using CanHub.Adapter.Virtual.Internal;

namespace CanHub.Adapter.Virtual;

/// <summary>
/// 虚拟适配器恢复测试选项。用于在测试中注入确定性的总线故障。<br/>
/// Virtual adapter recovery test options. Used to inject deterministic bus
/// faults in tests.
/// </summary>
public sealed class VirtualRecoveryOptions
{
    /// <summary>虚拟故障注入器。<br/>Virtual fault injector.</summary>
    public VirtualFaultInjector? FaultInjector { get; init; }
}

/// <summary>
/// 虚拟 CAN 故障注入器。调用方可手动触发 bus-off 状态。<br/>
/// Virtual CAN fault injector. Callers can manually trigger bus-off state.
/// </summary>
public sealed class VirtualFaultInjector
{
    private readonly object _gate = new();
    private readonly HashSet<VirtualChannelState> _channels = [];

    internal void Register(VirtualChannelState channelState)
    {
        lock (_gate)
            _channels.Add(channelState);
    }

    internal void Unregister(VirtualChannelState channelState)
    {
        lock (_gate)
            _channels.Remove(channelState);
    }

    /// <summary>向已注册虚拟通道注入 bus-off。<br/>Injects bus-off into registered virtual channels.</summary>
    public void InjectBusOff()
    {
        VirtualChannelState[] channels;
        lock (_gate)
            channels = _channels.ToArray();

        foreach (var channel in channels)
            channel.InjectBusOff();
    }
}
