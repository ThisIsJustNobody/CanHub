using System.Collections.Concurrent;
using CanHub.Adapter.Vector.Internal;
using vxlapi_NET;

namespace CanHub.Adapter.Vector;

/// <summary>
/// Vector CAN 逻辑总线会话。物理端口生命周期由共享通道租约管理。<br/>
/// Vector CAN logical bus session. Physical port lifecycle is managed by the shared channel lease.
/// </summary>
internal sealed class VectorBus : ICanBus
{
    private readonly VectorChannelLeaseEntry _entry;
    private readonly Action<VectorChannelLeaseEntry> _release;
    private readonly Func<VectorChannelLeaseEntry, CancellationToken, ValueTask> _releaseAsync;
    private readonly ConcurrentDictionary<Guid, ICanSubscription> _subscriptions = new();
    private readonly object _statusGate = new();
    private readonly List<Action<CanStatusEvent>> _statusHandlers = [];
    private int _disposed;

    internal VectorBus(
        VectorChannelLeaseEntry entry,
        Action<VectorChannelLeaseEntry> release,
        Func<VectorChannelLeaseEntry, CancellationToken, ValueTask> releaseAsync)
    {
        _entry = entry;
        _release = release;
        _releaseAsync = releaseAsync;
    }

    /// <inheritdoc />
    public string DisplayName => _entry.DisplayName;
    /// <inheritdoc />
    public bool IsOpen => Volatile.Read(ref _disposed) == 0 && _entry.IsOpen;

    /// <summary>
    /// 总线状态变更事件。订阅后立即回放已缓冲的诊断事件，保证无事件丢失。<br/>
    /// Bus status change event. Upon subscription, buffered diagnostic events are replayed
    /// immediately to ensure no events are lost.
    /// </summary>
    public event Action<CanStatusEvent>? StatusChanged
    {
        add
        {
            if (value is null)
                return;

            lock (_statusGate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                _statusHandlers.Add(value);
                _entry.StatusChanged += value;
            }
        }
        remove
        {
            if (value is null)
                return;

            lock (_statusGate)
            {
                var index = _statusHandlers.LastIndexOf(value);
                if (index < 0)
                    return;

                _statusHandlers.RemoveAt(index);
                _entry.StatusChanged -= value;
            }
        }
    }

    /// <summary>
    /// 发送 CAN 帧。验证帧类型和 FD 能力，拒绝未实现的选项配置，通过底层通道租约发送。<br/>
    /// Sends a CAN frame. Validates frame type and FD capability, rejects unimplemented
    /// option configurations, and transmits via the underlying channel lease.
    /// </summary>
    public ValueTask<CanTransmitSubmissionResult> SendAsync(
        CanFrame frame, CanTransmitOptions? options = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ct.ThrowIfCancellationRequested();

        if (!_entry.CanSubmitTransmit)
        {
            var rejectId = _entry.Hub.AllocateSequence();
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                rejectId, CanTransmitSubmissionStatus.NotStarted));
        }

        if (!frame.IsTransmittable)
        {
            var rejectId = _entry.Hub.AllocateSequence();
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                rejectId, CanTransmitSubmissionStatus.InvalidFrame));
        }

        if (!_entry.IsFd && frame.Flags.HasFlag(CanFrameFlags.FD))
        {
            var rejectId = _entry.Hub.AllocateSequence();
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                rejectId, CanTransmitSubmissionStatus.UnsupportedFeature));
        }

        // 拒绝未实现的选项配置
        if (options is not null && !IsDefaultOptions(options))
        {
            var rejectId = _entry.Hub.AllocateSequence();
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                rejectId, CanTransmitSubmissionStatus.UnsupportedFeature));
        }

        var correlationId = _entry.Hub.AllocateSequence();
        var status = _entry.Send(frame, correlationId);

        if (status == XLDefine.XL_Status.XL_SUCCESS)
            return ValueTask.FromResult(CanTransmitSubmissionResult.AcceptedResult(correlationId, (uint)status));

        PublishTransmitError(
            correlationId,
            (uint)status,
            $"Vector transmit failed: status={status} ({VectorDriver.Driver.XL_GetErrorString(status)}).");

        return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
            correlationId, CanTransmitSubmissionStatus.NativeError, (uint)status));
    }

    /// <summary>
    /// 批量发送 CAN 帧。迭代调用 SendAsync，拒绝未实现的选项配置。<br/>
    /// Sends a batch of CAN frames. Iteratively calls SendAsync, rejecting unimplemented
    /// option configurations.
    /// </summary>
    public async ValueTask<CanTransmitSubmissionResult[]> SendBatchAsync(
        ReadOnlyMemory<CanFrame> frames, CanTransmitOptions? options = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ct.ThrowIfCancellationRequested();

        // 拒绝未实现的选项配置
        if (options is not null && !IsDefaultOptions(options))
        {
            var rejected = new CanTransmitSubmissionResult[frames.Length];
            for (int i = 0; i < rejected.Length; i++)
            {
                var rejectId = _entry.Hub.AllocateSequence();
                rejected[i] = CanTransmitSubmissionResult.Failed(
                    rejectId, CanTransmitSubmissionStatus.UnsupportedFeature);
            }
            return rejected;
        }

        var results = new CanTransmitSubmissionResult[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            results[i] = await SendAsync(frames.Span[i], options, ct);
        return results;
    }

    /// <summary>
    /// 创建订阅以接收 CAN 帧事件。订阅通过共享广播集线器分配，受会话生命周期管理。<br/>
    /// Creates a subscription to receive CAN frame events. The subscription is allocated
    /// via the shared broadcast hub and managed within the session lifecycle.
    /// </summary>
    public ICanSubscription Subscribe(CanSubscriptionOptions options)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var subscription = _entry.Hub.Subscribe(options);
        var id = Guid.NewGuid();
        _subscriptions[id] = subscription;
        return new TrackedSubscription(subscription, this, id);
    }

    /// <summary>
    /// 同步释放会话。清理订阅和状态处理器，调用同步释放回调。
    /// 注意：内部使用同步阻塞等待异步释放操作完成，优先使用 <see cref="DisposeAsync"/>（await using 模式）。<br/>
    /// Synchronously disposes the session. Cleans up subscriptions and status handlers,
    /// and invokes the synchronous release callback. Note: internally blocks on async
    /// release completion; prefer <see cref="DisposeAsync"/> (await using pattern).
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        DisposeSubscriptions();
        DisposeStatusHandlers();
        _release(_entry);
    }

    private static bool IsDefaultOptions(CanTransmitOptions options) =>
        options.Mode == CanTransmitMode.Normal &&
        options.Completion == CanTransmitCompletion.SubmitOnly &&
        options.RetryPolicy == default &&
        !options.HighPriority;

    private void PublishTransmitError(
        ulong correlationId,
        uint nativeStatusCode,
        string message)
    {
        _entry.HandleFaultStatus(CanStatusEvent.Create(
            CanStatusKind.Transmit,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            sequence: _entry.Hub.AllocateSequence(),
            channelIndex: _entry.Key.ChannelIndex,
            correlationId: correlationId,
            nativeStatusCode: nativeStatusCode,
            message: message));
    }

    /// <summary>
    /// 异步释放会话。清理订阅和状态处理器，调用异步释放回调。<br/>
    /// Asynchronously disposes the session. Cleans up subscriptions and status handlers,
    /// and invokes the asynchronous release callback.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        DisposeSubscriptions();
        DisposeStatusHandlers();
        await _releaseAsync(_entry, CancellationToken.None).ConfigureAwait(false);
    }

    private void RemoveTrackedSubscription(Guid id)
    {
        _subscriptions.TryRemove(id, out _);
    }

    private void DisposeSubscriptions()
    {
        foreach (var subscription in _subscriptions.Values)
            subscription.Dispose();
        _subscriptions.Clear();
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
            _entry.StatusChanged -= handler;
    }

    /// <summary>
    /// 受跟踪的订阅包装器。将会话级订阅注销绑定到 Dispose 生命周期。<br/>
    /// Tracked subscription wrapper. Ties session-level subscription unregistration to the Dispose lifecycle.
    /// </summary>
    private sealed class TrackedSubscription : ICanSubscription
    {
        private readonly ICanSubscription _inner;
        private readonly VectorBus _session;
        private readonly Guid _id;
        private int _disposed;

        public TrackedSubscription(ICanSubscription inner, VectorBus session, Guid id)
        {
            _inner = inner;
            _session = session;
            _id = id;
        }

        /// <inheritdoc />
        public ValueTask<CanFrameEvent> ReadAsync(CancellationToken ct = default) =>
            _inner.ReadAsync(ct);

        /// <inheritdoc />
        public IAsyncEnumerable<CanFrameEvent> ReadAllAsync(CancellationToken ct = default) =>
            _inner.ReadAllAsync(ct);

        /// <inheritdoc />
        public CanSubscriptionStatistics Statistics => _inner.Statistics;

        /// <summary>
        /// 释放订阅。从会话的订阅追踪中注销自身。<br/>
        /// Disposes the subscription. Unregisters itself from the session's subscription tracking.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _session.RemoveTrackedSubscription(_id);
            _inner.Dispose();
        }
    }
}
