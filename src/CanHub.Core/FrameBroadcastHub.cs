using System.Collections.Concurrent;
using System.Threading.Channels;
using CanHub.Core.Internal;

namespace CanHub.Core;

/// <summary>
/// 帧广播中心。管理订阅者集合并将帧事件分发到所有订阅通道。<br/>
/// Frame broadcast hub. Manages the subscriber collection and distributes frame events to all subscribed channels.
/// </summary>
internal sealed class FrameBroadcastHub : IDisposable
{
    private readonly CanSequenceGenerator _sequenceGenerator;
    private readonly ConcurrentDictionary<Guid, ChannelSubscription> _subscriptions = new();
    private readonly object _gate = new();
    private int _disposed;

    public FrameBroadcastHub(CanSequenceGenerator sequenceGenerator)
    {
        _sequenceGenerator = sequenceGenerator;
    }

    /// <summary>
    /// 创建新的帧订阅。根据选项配置通道容量和溢写策略。<br/>
    /// Creates a new frame subscription. Configures channel capacity and overflow policy based on options.
    /// </summary>
    public ICanSubscription Subscribe(CanSubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var capacity = options.QueueCapacity > 0 ? options.QueueCapacity : 4096;

        var queueFullMode = options.FullMode switch
        {
            CanQueueFullMode.DropOldest => CanQueueFullMode.DropOldest,
            CanQueueFullMode.DropNewest => CanQueueFullMode.DropNewest,
            _ => CanQueueFullMode.DropOldest
        };

        var fullMode = queueFullMode switch
        {
            CanQueueFullMode.DropOldest => BoundedChannelFullMode.DropOldest,
            CanQueueFullMode.DropNewest => BoundedChannelFullMode.DropWrite,
            _ => BoundedChannelFullMode.DropOldest
        };

        var channel = Channel.CreateBounded<CanFrameEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = fullMode
        });

        var subscription = new ChannelSubscription(channel, this, capacity, queueFullMode);
        var id = Guid.NewGuid();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            _subscriptions[id] = subscription;
        }

        return subscription;
    }

    /// <summary>分配全局唯一的单调递增序列号。<br/>Allocates a globally unique monotonically increasing sequence number.</summary>
    public ulong AllocateSequence() => _sequenceGenerator.Allocate();

    /// <summary>
    /// 将帧事件广播到所有订阅通道。<br/>
    /// Broadcasts a frame event to all subscribed channels.
    /// </summary>
    public void Broadcast(CanFrameEvent frameEvent)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        foreach (var kvp in _subscriptions)
        {
            kvp.Value.TryWrite(frameEvent);
        }
    }

    /// <summary>
    /// 移除指定订阅（由 <see cref="ChannelSubscription"/> 的 Dispose 调用）。<br/>
    /// Removes the specified subscription (called by <see cref="ChannelSubscription"/>'s Dispose).
    /// </summary>
    internal void RemoveSubscription(ChannelSubscription subscription)
    {
        lock (_gate)
        {
            foreach (var kvp in _subscriptions)
            {
                if (ReferenceEquals(kvp.Value, subscription))
                {
                    _subscriptions.TryRemove(kvp.Key, out _);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 释放所有订阅并清空订阅集合。<br/>
    /// Disposes all subscriptions and clears the subscription collection.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            foreach (var kvp in _subscriptions)
            {
                kvp.Value.Complete();
            }
            _subscriptions.Clear();
        }
    }
}
