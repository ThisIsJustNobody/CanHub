using System.Collections.Concurrent;
using System.Threading;
using CanHub.Adapter.Virtual.Internal;

namespace CanHub.Adapter.Virtual;

/// <summary>
/// 虚拟 CAN 总线会话。在内存中模拟 CAN 总线通信，同一虚拟总线名称的通道之间可以互相通信。<br/>
/// Virtual CAN bus session. Simulates CAN bus communication in memory;
/// channels sharing the same virtual bus name can communicate with each other.
/// </summary>
internal sealed class VirtualBusSession : ICanBus
{
    private readonly VirtualBusGroup _group;
    private readonly VirtualChannelState _channelState;
    private readonly bool _fdEnabled;
    private readonly ConcurrentDictionary<Guid, ICanSubscription> _subscriptions = new();
    private readonly List<Action<CanStatusEvent>> _statusHandlers = [];
    private readonly object _statusGate = new();
    private int _disposed;

    /// <inheritdoc/>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public bool IsOpen => Volatile.Read(ref _disposed) == 0 && _channelState.IsOpen;

    /// <summary>
    /// 当前会话对应的虚拟通道索引。<br/>
    /// The virtual channel index for this session.
    /// </summary>
    public int ChannelIndex => _channelState.ChannelIndex;

    /// <summary>
    /// 虚拟总线状态变化事件。当前实现为空事件，预留扩展。<br/>
    /// Virtual bus status change event. Currently a no-op event reserved for future extension.
    /// </summary>
    public event Action<CanStatusEvent>? StatusChanged
    {
        add
        {
            if (value is null) return;
            lock (_statusGate)
                _statusHandlers.Add(value);
            _channelState.StatusChanged += value;
        }
        remove
        {
            if (value is null) return;
            lock (_statusGate)
                _statusHandlers.Remove(value);
            _channelState.StatusChanged -= value;
        }
    }

    public VirtualBusSession(VirtualBusGroup group, VirtualChannelState channelState, bool fdEnabled)
    {
        _group = group;
        _channelState = channelState;
        _fdEnabled = fdEnabled;
        DisplayName = $"Virtual-{group.BusName}-CH{channelState.ChannelIndex}";
    }

    /// <summary>
    /// 发送单个 CAN 帧。验证帧的可发送性、选项有效性和 FD 能力，
    /// 然后在同一虚拟总线组内的所有通道之间广播。<br/>
    /// Sends a single CAN frame. Validates frame transmittability, option validity,
    /// and FD capability, then broadcasts to all channels within the same virtual bus group.
    /// </summary>
    public ValueTask<CanTransmitSubmissionResult> SendAsync(
        CanFrame frame,
        CanTransmitOptions? options = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(!_channelState.IsOpen, this);

        if (!_channelState.CanSubmitTransmit)
        {
            var rejectId = _group.AllocateCorrelationId();
            return ValueTask.FromResult(
                CanTransmitSubmissionResult.Failed(rejectId, CanTransmitSubmissionStatus.NotStarted));
        }

        if (!frame.IsTransmittable)
        {
            var rejectId = _group.AllocateCorrelationId();
            return ValueTask.FromResult(
                CanTransmitSubmissionResult.Failed(rejectId, CanTransmitSubmissionStatus.InvalidFrame));
        }

        // 拒绝未实现的选项配置
        if (options is not null && !IsDefaultOptions(options))
        {
            var rejectId = _group.AllocateCorrelationId();
            return ValueTask.FromResult(
                CanTransmitSubmissionResult.Failed(rejectId, CanTransmitSubmissionStatus.UnsupportedFeature));
        }

        // 如果 FD 未启用但帧是 FD 帧，拒绝
        if (!_fdEnabled && frame.Flags.HasFlag(CanFrameFlags.FD))
        {
            var rejectedCorrelationId = _group.AllocateCorrelationId();
            return ValueTask.FromResult(
                CanTransmitSubmissionResult.Failed(
                    rejectedCorrelationId, CanTransmitSubmissionStatus.UnsupportedFeature));
        }

        var correlationId = _group.AllocateCorrelationId();
        _group.Transmit(this, frame, correlationId);
        return ValueTask.FromResult(CanTransmitSubmissionResult.AcceptedResult(correlationId));
    }

    /// <summary>
    /// 批量发送 CAN 帧。对每一帧依次验证帧可发送性、选项有效性和 FD 能力，
    /// 然后分别在同一虚拟总线组内的所有通道之间广播。<br/>
    /// Sends a batch of CAN frames. Validates transmittability, option validity,
    /// and FD capability for each frame, then broadcasts each to all channels
    /// within the same virtual bus group.
    /// </summary>
    public ValueTask<CanTransmitSubmissionResult[]> SendBatchAsync(
        ReadOnlyMemory<CanFrame> frames,
        CanTransmitOptions? options = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(!_channelState.IsOpen, this);

        // 拒绝未实现的选项配置
        if (options is not null && !IsDefaultOptions(options))
        {
            var rejected = new CanTransmitSubmissionResult[frames.Length];
            for (int i = 0; i < rejected.Length; i++)
            {
                var rejectId = _group.AllocateCorrelationId();
                rejected[i] = CanTransmitSubmissionResult.Failed(
                    rejectId, CanTransmitSubmissionStatus.UnsupportedFeature);
            }
            return ValueTask.FromResult(rejected);
        }

        if (!_channelState.CanSubmitTransmit)
        {
            var rejected = new CanTransmitSubmissionResult[frames.Length];
            for (int i = 0; i < rejected.Length; i++)
            {
                var rejectId = _group.AllocateCorrelationId();
                rejected[i] = CanTransmitSubmissionResult.Failed(
                    rejectId, CanTransmitSubmissionStatus.NotStarted);
            }
            return ValueTask.FromResult(rejected);
        }

        var span = frames.Span;
        var results = new CanTransmitSubmissionResult[span.Length];

        for (int i = 0; i < span.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (!span[i].IsTransmittable)
            {
                var correlationId = _group.AllocateCorrelationId();
                results[i] = CanTransmitSubmissionResult.Failed(correlationId, CanTransmitSubmissionStatus.InvalidFrame);
            }
            else if (!_fdEnabled && span[i].Flags.HasFlag(CanFrameFlags.FD))
            {
                var correlationId = _group.AllocateCorrelationId();
                results[i] = CanTransmitSubmissionResult.Failed(correlationId, CanTransmitSubmissionStatus.UnsupportedFeature);
            }
            else
            {
                var correlationId = _group.AllocateCorrelationId();
                _group.Transmit(this, span[i], correlationId);
                results[i] = CanTransmitSubmissionResult.AcceptedResult(correlationId);
            }
        }

        return ValueTask.FromResult(results);
    }

    /// <summary>
    /// 订阅虚拟总线通道上的帧事件。创建一个新的订阅并包装为 TrackedSubscription，
    /// 以便在会话释放时自动清理。<br/>
    /// Subscribes to frame events on the virtual bus channel. Creates a new subscription
    /// wrapped as a TrackedSubscription for automatic cleanup on session disposal.
    /// </summary>
    public ICanSubscription Subscribe(CanSubscriptionOptions options)
    {
        ObjectDisposedException.ThrowIf(!IsOpen, this);

        ICanSubscription subscription;
        try
        {
            subscription = _channelState.Hub.Subscribe(options);
        }
        catch (ObjectDisposedException)
        {
            // 与 Dispose 并发时 Hub 可能已被释放
            throw new ObjectDisposedException(nameof(VirtualBusSession));
        }
        var id = Guid.NewGuid();
        _subscriptions[id] = subscription;
        return new TrackedSubscription(subscription, this, id);
    }

    internal void RemoveTrackedSubscription(Guid id)
    {
        _subscriptions.TryRemove(id, out _);
    }

    /// <summary>
    /// 同步释放会话资源。释放所有订阅、从 VirtualBusStore 释放通道引用，
    /// 如果通道引用计数归零则清理通道和组。<br/>
    /// Synchronously disposes the session. Disposes all subscriptions and releases
    /// the channel reference from VirtualBusStore; cleans up the channel and group
    /// when the reference count reaches zero.
    /// </summary>
    public void Dispose()
    {
        DisposeInternal();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 异步释放会话资源。行为和同步释放一致。<br/>
    /// Asynchronously disposes the session. Behavior is identical to synchronous disposal.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        DisposeInternal();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void DisposeInternal()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // 释放所有订阅
        foreach (var sub in _subscriptions.Values)
            sub.Dispose();
        _subscriptions.Clear();
        DisposeStatusHandlers();

        // 释放通道引用；全局存储负责原子移除和最终 Dispose。
        VirtualBusStore.ReleaseChannel(_group, _channelState);
    }

    private void DisposeStatusHandlers()
    {
        Action<CanStatusEvent>[] handlers;
        lock (_statusGate)
        {
            if (_statusHandlers.Count == 0)
                return;

            handlers = _statusHandlers.ToArray();
            _statusHandlers.Clear();
        }

        foreach (var handler in handlers)
            _channelState.StatusChanged -= handler;
    }

    private static bool IsDefaultOptions(CanTransmitOptions options) =>
        options.Mode == CanTransmitMode.Normal &&
        options.Completion == CanTransmitCompletion.SubmitOnly &&
        options.RetryPolicy == default &&
        !options.HighPriority;

    /// <summary>
    /// 跟踪订阅包装。在释放时自动从父会话的订阅字典中移除自身。<br/>
    /// Tracked subscription wrapper. Automatically removes itself from the parent
    /// session's subscription dictionary upon disposal.
    /// </summary>
    private sealed class TrackedSubscription : ICanSubscription
    {
        private readonly ICanSubscription _inner;
        private readonly VirtualBusSession _session;
        private readonly Guid _id;
        private int _disposed;

        public TrackedSubscription(ICanSubscription inner, VirtualBusSession session, Guid id)
        {
            _inner = inner;
            _session = session;
            _id = id;
        }

        /// <inheritdoc/>
        public ValueTask<CanFrameEvent> ReadAsync(CancellationToken ct = default) =>
            _inner.ReadAsync(ct);

        /// <inheritdoc/>
        public IAsyncEnumerable<CanFrameEvent> ReadAllAsync(CancellationToken ct = default) =>
            _inner.ReadAllAsync(ct);

        /// <inheritdoc/>
        public CanSubscriptionStatistics Statistics => _inner.Statistics;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            _session.RemoveTrackedSubscription(_id);
            _inner.Dispose();
        }
    }
}
