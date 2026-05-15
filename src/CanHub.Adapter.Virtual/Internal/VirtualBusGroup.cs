using System.Collections.Concurrent;
using CanHub.Core;

namespace CanHub.Adapter.Virtual.Internal;

/// <summary>
/// 虚拟总线组。管理同一虚拟总线名称下的所有通道，负责在同一虚拟总线上的通道之间路由帧。<br/>
/// Virtual bus group. Manages all channels under the same virtual bus name
/// and routes frames between channels on the same virtual bus.
/// </summary>
internal sealed class VirtualBusGroup
{
    private readonly ConcurrentDictionary<int, VirtualChannelState> _channels = new();
    private readonly CanSequenceGenerator _sequenceGenerator = new();

    /// <summary>
    /// 虚拟总线名称。<br/>
    /// The virtual bus name.
    /// </summary>
    public string BusName { get; }

    public VirtualBusGroup(string busName)
    {
        BusName = busName;
    }

    /// <summary>
    /// 获取或创建指定索引的通道状态。线程安全。<br/>
    /// Gets or creates the channel state for the given index. Thread-safe.
    /// </summary>
    public VirtualChannelState GetOrAddChannel(int channelIndex) =>
        _channels.GetOrAdd(channelIndex, idx => new VirtualChannelState(idx, _sequenceGenerator));

    /// <summary>
    /// 移除指定通道。仅当当前存储的实例与传入实例引用相等时才移除。<br/>
    /// Removes the specified channel. Only removes when the stored instance
    /// is reference-equal to the given instance.
    /// </summary>
    public bool RemoveChannel(int channelIndex, VirtualChannelState channelState)
    {
        if (!_channels.TryGetValue(channelIndex, out var current) ||
            !ReferenceEquals(current, channelState))
        {
            return false;
        }

        return _channels.TryRemove(channelIndex, out _);
    }

    /// <summary>
    /// 指示组内是否没有任何通道。<br/>
    /// Whether the group has no channels.
    /// </summary>
    public bool IsEmpty => _channels.IsEmpty;

    /// <summary>
    /// 获取所有通道状态（用于测试清理）。<br/>
    /// Gets all channel states (used for test cleanup).
    /// </summary>
    internal IReadOnlyCollection<VirtualChannelState> GetAllChannels() => _channels.Values.ToArray();

    /// <summary>
    /// 分配一个全局唯一的 Correlation ID。<br/>
    /// Allocates a globally unique correlation ID.
    /// </summary>
    public ulong AllocateCorrelationId() => _sequenceGenerator.Allocate();

    /// <summary>
    /// 将帧广播到所有通道。发送者通道获得 TX 确认事件，其他通道获得 RX 事件。<br/>
    /// Broadcasts a frame to all channels. The sender channel receives a TX confirmation event;
    /// all other channels receive RX events.
    /// </summary>
    public void Transmit(VirtualBusSession sender, CanFrame frame, ulong correlationId)
    {
        foreach (var kvp in _channels)
        {
            var channelState = kvp.Value;
            var sequence = _sequenceGenerator.Allocate();

            try
            {
                if (kvp.Key == sender.ChannelIndex)
                {
                    // 发送者自己的通道：TX 确认事件
                    var txEvent = CanFrameEvent.CreateTransmitted(
                        correlationId,
                        frame,
                        CanTransmitOutcome.Transmitted,
                        sequence,
                        channelIndex: channelState.ChannelIndex);
                    channelState.Hub.Broadcast(txEvent);
                }
                else
                {
                    // 其他通道：RX 接收事件
                    var rxEvent = CanFrameEvent.CreateReceived(
                        frame,
                        sequence,
                        channelIndex: channelState.ChannelIndex);
                    channelState.Hub.Broadcast(rxEvent);
                }
            }
            catch (ObjectDisposedException)
            {
                // 通道可能在迭代期间被并发释放，安全跳过
            }
        }
    }
}
