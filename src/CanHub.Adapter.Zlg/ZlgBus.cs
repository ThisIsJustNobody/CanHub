using System.Collections.Concurrent;
using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg;

/// <summary>
/// ZLG CAN 逻辑总线会话。物理设备和通道生命周期由共享租约管理。<br/>
/// ZLG CAN logical bus session. Physical device and channel lifecycle are managed by shared leases.
/// </summary>
internal sealed class ZlgBus : ICanBus
{
    private readonly ZlgChannelLeaseEntry _entry;
    private readonly Action<ZlgChannelLeaseEntry> _release;
    private readonly Func<ZlgChannelLeaseEntry, CancellationToken, ValueTask> _releaseAsync;
    private readonly ConcurrentDictionary<Guid, ICanSubscription> _subscriptions = new();
    private readonly object _statusGate = new();
    private readonly List<Action<CanStatusEvent>> _statusHandlers = [];
    private int _disposed;

    internal ZlgBus(
        ZlgChannelLeaseEntry entry,
        Action<ZlgChannelLeaseEntry> release,
        Func<ZlgChannelLeaseEntry, CancellationToken, ValueTask> releaseAsync)
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
    /// 状态变更事件。直接委托给底层通道租约的状态变更事件，添加或移除处理器。<br/>
    /// Status change event. Delegates directly to the underlying channel lease, forwarding add/remove handler operations with disposal guard.
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
    /// 发送 CAN 帧到 ZLG 通道。验证帧类型，将帧转换为原生格式，调用 ZLG 驱动发送。<br/>
    /// Sends a CAN frame to the ZLG channel. Validates frame type, converts to native format, and sends via the ZLG driver.
    /// </summary>
    public ValueTask<CanTransmitSubmissionResult> SendAsync(
        CanFrame frame,
        CanTransmitOptions? options = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ct.ThrowIfCancellationRequested();

        if (!_entry.CanSubmitTransmit)
        {
            var rejectId = _entry.HubAllocateSequence();
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                rejectId, CanTransmitSubmissionStatus.NotStarted));
        }

        if (!frame.IsTransmittable)
        {
            var rejectId = _entry.HubAllocateSequence();
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                rejectId, CanTransmitSubmissionStatus.InvalidFrame));
        }

        if (!_entry.IsFd && frame.Flags.HasFlag(CanFrameFlags.FD))
        {
            var rejectId = _entry.HubAllocateSequence();
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                rejectId, CanTransmitSubmissionStatus.UnsupportedFeature));
        }

        if (options is not null && !IsDefaultOptions(options))
        {
            var rejectId = _entry.HubAllocateSequence();
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                rejectId, CanTransmitSubmissionStatus.UnsupportedFeature));
        }

        var correlationId = _entry.HubAllocateSequence();
        uint submitted;
        try
        {
            submitted = _entry.Send(frame);
        }
        catch (ZlgApiException ex)
        {
            PublishTransmitError(correlationId, (uint)ex.Status, 0, ex.Message);
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                correlationId,
                CanTransmitSubmissionStatus.NativeError,
                nativeStatusCode: (uint)ex.Status));
        }
        catch (Exception ex) when (ZlgExceptionMapper.IsNativeBoundaryException(ex))
        {
            var mapped = ZlgExceptionMapper.ToCanException(ex);
            PublishTransmitError(correlationId, 0, 0, mapped.Message);
            return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
                correlationId,
                CanTransmitSubmissionStatus.NativeError));
        }

        if (submitted == 1)
            return ValueTask.FromResult(CanTransmitSubmissionResult.AcceptedResult(correlationId, submitted));

        PublishTransmitError(
            correlationId,
            submitted,
            0,
            $"ZLG transmit returned {submitted}; expected 1 submitted frame.");
        return ValueTask.FromResult(CanTransmitSubmissionResult.Failed(
            correlationId,
            CanTransmitSubmissionStatus.NativeError,
            nativeStatusCode: submitted));
    }

    /// <summary>
    /// 批量发送 CAN 帧。逐帧调用 SendAsync，仅支持默认选项。<br/>
    /// Sends CAN frames in batch. Delegates to SendAsync per frame; only default options are supported.
    /// </summary>
    public async ValueTask<CanTransmitSubmissionResult[]> SendBatchAsync(
        ReadOnlyMemory<CanFrame> frames,
        CanTransmitOptions? options = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ct.ThrowIfCancellationRequested();

        if (frames.IsEmpty)
            return Array.Empty<CanTransmitSubmissionResult>();

        if (options is not null && !IsDefaultOptions(options))
        {
            var rejected = new CanTransmitSubmissionResult[frames.Length];
            for (var i = 0; i < rejected.Length; i++)
            {
                var rejectId = _entry.HubAllocateSequence();
                rejected[i] = CanTransmitSubmissionResult.Failed(
                    rejectId, CanTransmitSubmissionStatus.UnsupportedFeature);
            }

            return rejected;
        }

        var results = new CanTransmitSubmissionResult[frames.Length];
        for (var i = 0; i < frames.Length; i++)
            results[i] = await SendAsync(frames.Span[i], options, ct).ConfigureAwait(false);
        return results;
    }

    /// <summary>
    /// 订阅 CAN 帧事件。创建广播中心订阅并包装为跟踪订阅，确保会话关闭时自动取消。<br/>
    /// Subscribes to CAN frame events. Creates a hub subscription wrapped as a tracked subscription to ensure automatic cleanup on session close.
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
    /// 释放总线会话。清理订阅和状态处理器，同步释放通道租约引用。<br/>
    /// Disposes the bus session. Cleans up subscriptions and status handlers, then synchronously releases the channel lease reference.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        DisposeSubscriptions();
        DisposeStatusHandlers();
        _release(_entry);
    }

    /// <summary>
    /// 异步释放总线会话。清理订阅和状态处理器，异步释放通道租约引用。<br/>
    /// Asynchronously disposes the bus session. Cleans up subscriptions and status handlers, then releases the channel lease reference asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        DisposeSubscriptions();
        DisposeStatusHandlers();
        await _releaseAsync(_entry, CancellationToken.None).ConfigureAwait(false);
    }

    private void PublishTransmitError(
        ulong correlationId,
        uint nativeStatusCode,
        uint nativeErrorCode,
        string message)
    {
        _entry.HandleFaultStatus(CanStatusEvent.Create(
            CanStatusKind.Transmit,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            sequence: _entry.HubAllocateSequence(),
            channelIndex: _entry.Key.ChannelIndex,
            correlationId: correlationId,
            nativeStatusCode: nativeStatusCode,
            nativeErrorCode: nativeErrorCode,
            message: message));
    }

    private static bool IsDefaultOptions(CanTransmitOptions options) =>
        options.Mode == CanTransmitMode.Normal &&
        options.Completion == CanTransmitCompletion.SubmitOnly &&
        options.RetryPolicy == CanTransmitRetryPolicy.None &&
        !options.HighPriority;

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
    /// 跟踪订阅包装器。在底层订阅之上提供会话级生命周期跟踪，确保会话关闭时从会话列表中移除。<br/>
    /// Tracked subscription wrapper. Provides session-level lifecycle tracking on top of the inner subscription, ensuring removal from the session list on close.
    /// </summary>
    private sealed class TrackedSubscription : ICanSubscription
    {
        private readonly ICanSubscription _inner;
        private readonly ZlgBus _session;
        private readonly Guid _id;
        private int _disposed;

        public TrackedSubscription(ICanSubscription inner, ZlgBus session, Guid id)
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
        /// 释放跟踪订阅。从会话列表中移除此订阅并释放底层订阅。<br/>
        /// Disposes the tracked subscription. Removes this subscription from the session list and disposes the inner subscription.
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
