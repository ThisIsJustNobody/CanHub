using CanHub.Core;

namespace CanHub.Adapter.Virtual.Internal;

/// <summary>
/// 虚拟通道状态。维护通道级别的引用计数和帧广播 Hub。<br/>
/// Virtual channel state. Maintains channel-level reference counting and a frame broadcast hub.
/// </summary>
internal sealed class VirtualChannelState
{
    private int _referenceCount;

    /// <summary>
    /// 通道索引。<br/>
    /// The channel index.
    /// </summary>
    public int ChannelIndex { get; }

    /// <summary>
    /// 帧广播 Hub，用于向本通道订阅者分发帧事件。<br/>
    /// Frame broadcast hub for distributing frame events to subscribers on this channel.
    /// </summary>
    public FrameBroadcastHub Hub { get; }

    /// <summary>
    /// 当前引用计数。<br/>
    /// The current reference count.
    /// </summary>
    public int ReferenceCount => _referenceCount;

    public VirtualChannelState(int channelIndex, CanSequenceGenerator sequenceGenerator)
    {
        ChannelIndex = channelIndex;
        Hub = new FrameBroadcastHub(sequenceGenerator);
    }

    /// <summary>
    /// 增加引用计数。线程安全。<br/>
    /// Increments the reference count. Thread-safe.
    /// </summary>
    public void AddReference() => Interlocked.Increment(ref _referenceCount);

    /// <summary>
    /// 释放引用，返回释放后的引用计数。线程安全。<br/>
    /// Releases a reference, returning the count after release. Thread-safe.
    /// </summary>
    public int ReleaseReference() => Interlocked.Decrement(ref _referenceCount);

    /// <summary>
    /// 释放通道资源，包括 Hub。<br/>
    /// Disposes channel resources, including the Hub.
    /// </summary>
    public void Dispose()
    {
        Hub.Dispose();
    }
}
