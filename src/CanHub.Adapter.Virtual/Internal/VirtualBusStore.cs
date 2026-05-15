using System.Collections.Concurrent;

namespace CanHub.Adapter.Virtual.Internal;

/// <summary>
/// 虚拟总线全局存储。管理所有 VirtualBusGroup 实例的生命周期，
/// 提供通道的获取和释放操作。<br/>
/// Global store for virtual buses. Manages the lifecycle of all VirtualBusGroup
/// instances and provides channel acquisition and release operations.
/// </summary>
internal static class VirtualBusStore
{
    private static readonly ConcurrentDictionary<string, VirtualBusGroup> s_groups = new(StringComparer.Ordinal);
    private static readonly object s_gate = new();

    /// <summary>
    /// 获取或创建指定总线名称和通道索引的通道。如果总线组不存在则自动创建，
    /// 并为通道增加引用计数。<br/>
    /// Acquires or creates a channel for the given bus name and channel index.
    /// Automatically creates the bus group if it does not exist and increments the channel reference count.
    /// </summary>
    public static (VirtualBusGroup Group, VirtualChannelState ChannelState) AcquireChannel(
        string busName,
        int channelIndex)
    {
        lock (s_gate)
        {
            var group = s_groups.GetOrAdd(busName, name => new VirtualBusGroup(name));
            var channelState = group.GetOrAddChannel(channelIndex);
            channelState.AddReference();
            return (group, channelState);
        }
    }

    /// <summary>
    /// 释放通道引用。递减引用计数；当引用计数归零时从组中移除通道，
    /// 如果组变为空则同时移除组，最后释放通道资源。<br/>
    /// Releases a channel reference. Decrements the reference count; when it reaches zero,
    /// removes the channel from the group, removes the group if it becomes empty,
    /// and finally disposes the channel resources.
    /// </summary>
    public static void ReleaseChannel(VirtualBusGroup group, VirtualChannelState channelState)
    {
        VirtualChannelState? stateToDispose = null;

        lock (s_gate)
        {
            var remaining = channelState.ReleaseReference();
            if (remaining > 0)
                return;

            if (group.RemoveChannel(channelState.ChannelIndex, channelState))
            {
                stateToDispose = channelState;
                if (group.IsEmpty)
                    s_groups.TryRemove(group.BusName, out _);
            }
        }

        stateToDispose?.Dispose();
    }

    /// <summary>
    /// 仅用于测试的清理方法。释放所有通道资源并移除所有虚拟总线组。<br/>
    /// Test-only cleanup method. Disposes all channel resources and removes all virtual bus groups.
    /// </summary>
    internal static void Clear()
    {
        lock (s_gate)
        {
            foreach (var key in s_groups.Keys.ToArray())
            {
                if (s_groups.TryRemove(key, out var group))
                {
                    foreach (var channel in group.GetAllChannels())
                        channel.Dispose();
                }
            }
        }
    }
}
