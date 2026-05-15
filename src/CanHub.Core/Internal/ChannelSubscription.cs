using System.Threading.Channels;

namespace CanHub.Core.Internal;

/// <summary>
/// 通道订阅。在锁内维护有界帧缓冲和等待读取者，保证容量统计与实际队列状态一致。<br/>
/// Channel subscription. Maintains bounded frame buffering and pending readers under a lock
/// so capacity statistics stay consistent with the actual queue state.
/// </summary>
internal sealed class ChannelSubscription : ICanSubscription
{
    private readonly FrameBroadcastHub _hub;
    private readonly int _capacity;
    private readonly CanQueueFullMode _fullMode;
    private readonly object _gate = new();
    private readonly Queue<CanFrameEvent> _buffer = new();
    private readonly LinkedList<PendingRead> _pendingReads = new();
    private int _disposed;
    private ulong _droppedCount;
    private ulong _totalWriteCount; // 所有成功写入的帧数（含 DropOldest 模式下被立即丢弃的帧）
    private ulong _readCount;
    private ulong _lastDroppedSequence;

    public ChannelSubscription(
        Channel<CanFrameEvent> channel,
        FrameBroadcastHub hub,
        int capacity,
        CanQueueFullMode fullMode)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _hub = hub;
        _capacity = capacity;
        _fullMode = fullMode;
    }

    /// <inheritdoc/>
    public ValueTask<CanFrameEvent> ReadAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ValueTask.FromException<CanFrameEvent>(new OperationCanceledException(ct));

        PendingRead pendingRead;

        lock (_gate)
        {
            if (_buffer.Count > 0)
            {
                var item = _buffer.Dequeue();
                _readCount++;
                return ValueTask.FromResult(item);
            }

            if (_disposed != 0)
                return ValueTask.FromException<CanFrameEvent>(new ChannelClosedException());

            pendingRead = new PendingRead(this, ct);
            pendingRead.Node = _pendingReads.AddLast(pendingRead);
        }

        if (ct.CanBeCanceled)
        {
            var registration = ct.UnsafeRegister(
                static state => ((PendingRead)state!).CancelFromToken(),
                pendingRead);

            var disposeRegistration = false;
            lock (_gate)
            {
                pendingRead.CancellationRegistration = registration;
                disposeRegistration = pendingRead.IsCompleted;
            }

            if (disposeRegistration)
                registration.Dispose();
        }

        return new ValueTask<CanFrameEvent>(pendingRead.Task);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<CanFrameEvent> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (true)
        {
            CanFrameEvent item;
            try
            {
                item = await ReadAsync(ct).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                yield break;
            }

            yield return item;
        }
    }

    /// <inheritdoc/>
    public CanSubscriptionStatistics Statistics
    {
        get
        {
            lock (_gate)
            {
                return CanSubscriptionStatistics.Create(
                    capacity: _capacity,
                    bufferedCount: _buffer.Count,
                    droppedCount: _droppedCount,
                    lastDroppedSequence: _lastDroppedSequence,
                    enqueuedCount: _totalWriteCount,
                    readCount: _readCount);
            }
        }
    }

    internal int BufferedCount
    {
        get
        {
            lock (_gate)
            {
                return _buffer.Count;
            }
        }
    }

    internal int Capacity => _capacity;

    internal CanQueueFullMode FullMode => _fullMode;

    /// <summary>
    /// 尝试将帧写入通道。根据 <see cref="CanQueueFullMode"/> 处理队列满的情况：DropOldest 丢弃最旧帧，DropNewest 拒绝新帧。<br/>
    /// Attempts to write a frame to the channel. Handles queue-full scenarios based on <see cref="CanQueueFullMode"/>: DropOldest drops the oldest frame, DropNewest rejects the new frame.
    /// </summary>
    internal bool TryWrite(CanFrameEvent frameEvent)
    {
        PendingRead? pendingRead = null;

        lock (_gate)
        {
            if (_disposed != 0)
                return false;

            pendingRead = TakePendingRead();
            if (pendingRead is not null)
            {
                pendingRead.IsCompleted = true;
                _totalWriteCount++;
                _readCount++;
            }
            else
            {
                if (_buffer.Count >= _capacity)
                {
                    if (_fullMode == CanQueueFullMode.DropNewest)
                    {
                        _droppedCount++;
                        _lastDroppedSequence = frameEvent.Sequence;
                        return true;
                    }

                    var dropped = _buffer.Dequeue();
                    _droppedCount++;
                    _lastDroppedSequence = dropped.Sequence;
                }

                _buffer.Enqueue(frameEvent);
                _totalWriteCount++;
            }
        }

        pendingRead?.SetResult(frameEvent);
        return true;
    }

    /// <summary>
    /// 完成通道写入端。由 <see cref="FrameBroadcastHub"/> 在释放时调用，不解除订阅关联。<br/>
    /// Completes the channel writer. Called by <see cref="FrameBroadcastHub"/> during disposal; does not remove the subscription association.
    /// </summary>
    internal void Complete()
    {
        if (TryComplete(out var pendingReads))
            SetClosed(pendingReads);
    }

    /// <summary>
    /// 释放订阅资源。从广播中心移除订阅并完成通道。<br/>
    /// Disposes subscription resources. Removes the subscription from the broadcast hub and completes the channel.
    /// </summary>
    public void Dispose()
    {
        if (!TryComplete(out var pendingReads))
            return;

        _hub.RemoveSubscription(this);
        SetClosed(pendingReads);
    }

    private bool TryComplete(out List<PendingRead>? pendingReads)
    {
        lock (_gate)
        {
            if (_disposed != 0)
            {
                pendingReads = null;
                return false;
            }

            _disposed = 1;
            pendingReads = DrainPendingReads();
            return true;
        }
    }

    private PendingRead? TakePendingRead()
    {
        while (_pendingReads.First is { } node)
        {
            _pendingReads.RemoveFirst();
            var pendingRead = node.Value;
            pendingRead.Node = null;

            if (!pendingRead.IsCompleted)
                return pendingRead;
        }

        return null;
    }

    private List<PendingRead>? DrainPendingReads()
    {
        if (_pendingReads.Count == 0)
            return null;

        var pendingReads = new List<PendingRead>(_pendingReads.Count);
        while (_pendingReads.First is { } node)
        {
            _pendingReads.RemoveFirst();
            var pendingRead = node.Value;
            pendingRead.Node = null;

            if (pendingRead.IsCompleted)
                continue;

            pendingRead.IsCompleted = true;
            pendingReads.Add(pendingRead);
        }

        return pendingReads;
    }

    private void CancelPendingRead(PendingRead pendingRead)
    {
        var cancel = false;

        lock (_gate)
        {
            if (!pendingRead.IsCompleted)
            {
                pendingRead.IsCompleted = true;
                if (pendingRead.Node?.List is not null)
                {
                    _pendingReads.Remove(pendingRead.Node);
                    pendingRead.Node = null;
                }

                cancel = true;
            }
        }

        if (cancel)
            pendingRead.SetCanceled();
    }

    private static void SetClosed(List<PendingRead>? pendingReads)
    {
        if (pendingReads is null)
            return;

        foreach (var pendingRead in pendingReads)
            pendingRead.SetClosed();
    }

    private sealed class PendingRead
    {
        private readonly ChannelSubscription _owner;
        private readonly CancellationToken _cancellationToken;
        private readonly TaskCompletionSource<CanFrameEvent> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingRead(ChannelSubscription owner, CancellationToken cancellationToken)
        {
            _owner = owner;
            _cancellationToken = cancellationToken;
        }

        public Task<CanFrameEvent> Task => _completion.Task;

        public LinkedListNode<PendingRead>? Node { get; set; }

        public CancellationTokenRegistration CancellationRegistration { get; set; }

        public bool IsCompleted { get; set; }

        public void CancelFromToken() => _owner.CancelPendingRead(this);

        public void SetResult(CanFrameEvent frameEvent)
        {
            _completion.TrySetResult(frameEvent);
            CancellationRegistration.Dispose();
        }

        public void SetClosed()
        {
            _completion.TrySetException(new ChannelClosedException());
            CancellationRegistration.Dispose();
        }

        public void SetCanceled()
        {
            _completion.TrySetException(new OperationCanceledException(_cancellationToken));
        }
    }
}
