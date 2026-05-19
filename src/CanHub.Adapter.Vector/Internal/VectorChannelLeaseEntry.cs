using CanHub.Core;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 通道租约条目。管理单个物理通道的共享状态，包括驱动引用、端口、广播集线器和接收循环。<br/>
/// Vector channel lease entry. Manages shared state for a single physical channel, including
/// driver reference, port, broadcast hub, and receive loop.
/// </summary>
internal sealed class VectorChannelLeaseEntry : IAsyncDisposable
{
    private readonly VectorChannelOpenSpec _openSpec;
    private readonly IVectorChannelLifecycle _lifecycle;
    private readonly List<CanStatusEvent> _pendingDiagnostics = [];
    private readonly object _statusGate = new();
    private event Action<CanStatusEvent>? _statusChanged;
    private VectorReceiveLoop _receiveLoop;
    private CanRecoveryOptions _recovery;
    private int _referenceCount;
    private int _disposeState;
    private int _receiveLoopStarted;
    private int _recoveryInProgress;

    /// <summary>
    /// 创建通道租约条目。初始化时引用计数为 1。<br/>
    /// Creates a channel lease entry. Initializes with a reference count of 1.
    /// </summary>
    public VectorChannelLeaseEntry(
        VectorChannelKey key,
        VectorDriver driver,
        VectorChannelPort port,
        FrameBroadcastHub hub,
        byte[] fingerprint,
        bool isFd,
        string displayName,
        VectorChannelOpenSpec openSpec,
        CanRecoveryOptions recovery,
        IVectorChannelLifecycle lifecycle)
    {
        ArgumentNullException.ThrowIfNull(openSpec);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(lifecycle);

        Key = key;
        Driver = driver;
        Port = port;
        Hub = hub;
        Fingerprint = fingerprint;
        IsFd = isFd;
        DisplayName = displayName;
        _openSpec = openSpec;
        _recovery = recovery;
        _lifecycle = lifecycle;
        _receiveLoop = CreateReceiveLoop();
        _referenceCount = 1;
    }

    /// <summary>通道标识键。</summary>
    public VectorChannelKey Key { get; }
    /// <summary>共享驱动实例。</summary>
    public VectorDriver Driver { get; }
    /// <summary>通道端口。</summary>
    public VectorChannelPort Port { get; }
    /// <summary>帧广播集线器。</summary>
    public FrameBroadcastHub Hub { get; }
    /// <summary>异步接收循环。</summary>
    public VectorReceiveLoop ReceiveLoop => _receiveLoop;
    /// <summary>配置指纹，用于冲突检测。</summary>
    public byte[] Fingerprint { get; }
    /// <summary>是否为 CAN FD 通道。</summary>
    public bool IsFd { get; }
    /// <summary>会话显示名称。</summary>
    public string DisplayName { get; }
    /// <summary>当前引用计数。</summary>
    public int ReferenceCount => Volatile.Read(ref _referenceCount);
    /// <summary>端口是否已打开。</summary>
    public bool IsOpen =>
        Volatile.Read(ref _disposeState) == 0 &&
        Port.IsOpen &&
        !IsRecovering;
    /// <summary>通道是否正在自动恢复。</summary>
    public bool IsRecovering => Volatile.Read(ref _recoveryInProgress) != 0;
    /// <summary>当前是否允许提交发送。</summary>
    public bool CanSubmitTransmit =>
        Volatile.Read(ref _disposeState) == 0 &&
        Port.IsOpen &&
        (!IsRecovering || !RejectTransmitsWhileRecovering);
    /// <summary>是否正在关闭或已释放。</summary>
    public bool IsClosingOrDisposed => Volatile.Read(ref _disposeState) != 0;

    public event Action<CanStatusEvent>? StatusChanged
    {
        add
        {
            if (value is null) return;
            CanStatusEvent[] pending;
            lock (_statusGate)
            {
                _statusChanged += value;
                pending = _pendingDiagnostics.ToArray();
                _pendingDiagnostics.Clear();
            }

            foreach (var diag in pending)
                InvokeStatusHandler(value, diag);
        }
        remove
        {
            if (value is null) return;
            lock (_statusGate)
            {
                _statusChanged -= value;
            }
        }
    }

    /// <summary>
    /// 尝试增加引用计数。若条目正在关闭或已释放则失败。<br/>
    /// Attempts to increment the reference count. Fails if the entry is closing or already disposed.
    /// </summary>
    public bool TryAddReference()
    {
        if (IsClosingOrDisposed)
            return false;

        Interlocked.Increment(ref _referenceCount);
        if (!IsClosingOrDisposed)
            return true;

        ReleaseReference();
        return false;
    }

    /// <summary>
    /// 更新共享通道的恢复策略。非禁用策略会覆盖既有策略，禁用策略不会关闭已启用恢复。<br/>
    /// Updates the shared channel recovery policy. Non-disabled policies replace the existing one; disabled does not turn off an enabled policy.
    /// </summary>
    public void ConfigureRecovery(CanRecoveryOptions recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        lock (_statusGate)
        {
            if (_recovery == CanRecoveryOptions.Disabled || recovery.Mode != CanRecoveryMode.Disabled)
                _recovery = recovery;
        }
    }

    /// <summary>递减引用计数。</summary>
    public int ReleaseReference() => Interlocked.Decrement(ref _referenceCount);

    /// <summary>标记条目为关闭中状态。</summary>
    public void MarkClosing() => Interlocked.CompareExchange(ref _disposeState, 1, 0);

    /// <summary>启动接收循环。</summary>
    public void StartReceiveLoop()
    {
        Volatile.Write(ref _receiveLoopStarted, 1);
        ReceiveLoop.Start(IsFd);
    }

    /// <summary>
    /// 发送 CAN 帧。根据端口类型选择 CAN FD 或经典 CAN 发送路径。<br/>
    /// Sends a CAN frame. Selects the CAN FD or classic CAN transmit path based on port type.
    /// </summary>
    public XLDefine.XL_Status Send(CanFrame frame, ulong correlationId)
    {
        if (Port.IsFd)
        {
            var txEvent = VectorFrameConverter.ToCanFdTxEvent(frame, Port.NativeChannelIndex);
            uint sent = 0;
            return ReceiveLoop.TransmitCanFd(correlationId, ref sent, txEvent);
        }

        // 经典 CAN 通过 ReceiveLoop 发送以记录 correlationId，
        // 使 TX_COMPLETED 回显能够关联到原始发送请求
        var xlEvent = VectorFrameConverter.ToXlEvent(frame);
        return ReceiveLoop.TransmitClassic(correlationId, xlEvent);
    }

    /// <summary>
    /// 发布状态事件。若无订阅者则缓冲事件，有订阅者时直接分发。<br/>
    /// Publishes a status event. Buffers events when there are no subscribers; dispatches
    /// directly when subscribers exist.
    /// </summary>
    public void PublishStatus(CanStatusEvent statusEvent)
    {
        Delegate[]? handlers;
        lock (_statusGate)
        {
            if (_statusChanged is null)
            {
                _pendingDiagnostics.Add(statusEvent);
                return;
            }

            handlers = _statusChanged.GetInvocationList();
        }

        foreach (Action<CanStatusEvent> handler in handlers)
            InvokeStatusHandler(handler, statusEvent);
    }

    /// <summary>
    /// 发布故障状态，并在恢复策略匹配时启动自动恢复。<br/>
    /// Publishes a fault status and starts automatic recovery when the configured policy matches.
    /// </summary>
    public void HandleFaultStatus(CanStatusEvent statusEvent)
    {
        PublishStatus(statusEvent);

        var trigger = MapRecoveryTrigger(statusEvent);
        if (trigger == CanRecoveryTrigger.None)
            return;

        CanRecoveryOptions recovery;
        lock (_statusGate)
            recovery = _recovery;

        if (recovery.Mode == CanRecoveryMode.Disabled ||
            (recovery.Triggers & trigger) == CanRecoveryTrigger.None)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _recoveryInProgress, 1, 0) != 0)
        {
            PublishStatus(CanStatusEvent.Create(
                CanStatusKind.Bus,
                CanStatusCode.RecoverySkipped,
                CanStatusSeverity.Warning,
                sequence: Hub.AllocateSequence(),
                channelIndex: Key.ChannelIndex,
                message: "Vector recovery is already running."));
            return;
        }

        _ = Task.Run(() => RecoverAsync(recovery));
    }

    /// <summary>
    /// 同步释放租约条目。停止接收循环，释放端口、集线器和驱动引用。
    /// 若接收循环未能在超时内停止则返回 false。<br/>
    /// Synchronously disposes the lease entry. Stops the receive loop, releases the port,
    /// hub, and driver reference. Returns false if the receive loop fails to stop within the timeout.
    /// </summary>
    public bool Dispose()
    {
        var state = Volatile.Read(ref _disposeState);
        if (state == 2)
            return true;
        if (state == 0)
            Interlocked.CompareExchange(ref _disposeState, 1, 0);

        if (!ReceiveLoop.Stop())
            return false;
        Volatile.Write(ref _receiveLoopStarted, 0);

        Port.Dispose(PublishStatus);
        Hub.Dispose();
        Driver.Release();
        Volatile.Write(ref _disposeState, 2);
        return true;
    }

    /// <summary>
    /// 异步释放租约条目。<br/>
    /// Asynchronously disposes the lease entry.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task RecoverAsync(CanRecoveryOptions recovery)
    {
        try
        {
            await DelayIfNeededAsync(recovery.FaultDwellTime).ConfigureAwait(false);
            if (IsClosingOrDisposed)
                return;

            PublishStatus(CanStatusEvent.Create(
                CanStatusKind.Bus,
                CanStatusCode.Recovering,
                CanStatusSeverity.Warning,
                sequence: Hub.AllocateSequence(),
                channelIndex: Key.ChannelIndex,
                message: $"Vector recovery started: {recovery.Mode}."));

            switch (recovery.Mode)
            {
                case CanRecoveryMode.CloseOnFault:
                    CloseAfterFault();
                    return;

                case CanRecoveryMode.ResetOnFault:
                    await ReopenOnceAsync(recovery).ConfigureAwait(false);
                    return;

                case CanRecoveryMode.ReopenWithBackoff:
                    await ReopenWithBackoffAsync(recovery).ConfigureAwait(false);
                    return;
            }
        }
        finally
        {
            if (Port.IsOpen && !IsClosingOrDisposed)
                Volatile.Write(ref _recoveryInProgress, 0);
        }
    }

    private void CloseAfterFault()
    {
        if (ClosePortForRecovery(out _))
        {
            MarkClosing();
            PublishStatus(CanStatusEvent.Create(
                CanStatusKind.Channel,
                CanStatusCode.Disconnected,
                CanStatusSeverity.Warning,
                sequence: Hub.AllocateSequence(),
                channelIndex: Key.ChannelIndex,
                message: "Vector channel closed after bus fault."));
            return;
        }

        PublishRecoveryFailed(0, "Vector channel close failed during CloseOnFault recovery.");
    }

    private async Task ReopenOnceAsync(CanRecoveryOptions recovery)
    {
        if (!ClosePortForRecovery(out var restartReceiveLoop))
        {
            PublishRecoveryFailed(1, "Vector channel close failed before reset reopen.");
            return;
        }

        await DelayIfNeededAsync(recovery.RestartDelay).ConfigureAwait(false);
        if (IsClosingOrDisposed)
            return;

        try
        {
            await OpenPortAfterRecoveryAsync(restartReceiveLoop).ConfigureAwait(false);
            PublishRecovered(1);
        }
        catch (Exception ex) when (IsRecoveryOpenException(ex))
        {
            MarkClosing();
            PublishRecoveryFailed(1, ex.Message);
        }
    }

    private async Task ReopenWithBackoffAsync(CanRecoveryOptions recovery)
    {
        if (!ClosePortForRecovery(out var restartReceiveLoop))
        {
            PublishRecoveryFailed(0, "Vector channel close failed before backoff reopen.");
            return;
        }

        var attempts = Math.Max(1, recovery.MaxAttempts);
        var delay = recovery.RestartDelay;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await DelayIfNeededAsync(delay).ConfigureAwait(false);
            if (IsClosingOrDisposed)
                return;

            try
            {
                await OpenPortAfterRecoveryAsync(restartReceiveLoop).ConfigureAwait(false);
                PublishRecovered((ulong)attempt);
                return;
            }
            catch (Exception ex) when (IsRecoveryOpenException(ex))
            {
                lastException = ex;
                delay = NextBackoffDelay(delay, recovery.MaxBackoffDelay);
            }
        }

        MarkClosing();
        PublishRecoveryFailed((ulong)attempts, lastException?.Message ?? "Vector channel reopen failed.");
    }

    private bool ClosePortForRecovery(out bool restartReceiveLoop)
    {
        restartReceiveLoop = Volatile.Read(ref _receiveLoopStarted) != 0;
        if (restartReceiveLoop)
        {
            if (!ReceiveLoop.Stop())
                return false;

            Volatile.Write(ref _receiveLoopStarted, 0);
            _receiveLoop = CreateReceiveLoop();
        }

        return _lifecycle.ClosePort(Port, PublishStatus);
    }

    private async ValueTask OpenPortAfterRecoveryAsync(bool restartReceiveLoop)
    {
        await _lifecycle.OpenPortAsync(Port, _openSpec).ConfigureAwait(false);
        if (restartReceiveLoop)
            StartReceiveLoop();
    }

    private VectorReceiveLoop CreateReceiveLoop() =>
        new(Port, Hub, HandleFaultStatus, Port.TransmitEchoEnabled);

    private void PublishRecovered(ulong attemptCount)
    {
        if (Port.IsOpen && !IsClosingOrDisposed)
            Volatile.Write(ref _recoveryInProgress, 0);

        PublishStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.Recovered,
            CanStatusSeverity.Info,
            sequence: Hub.AllocateSequence(),
            channelIndex: Key.ChannelIndex,
            count: attemptCount,
            message: "Vector channel reopened after bus fault."));
    }

    private void PublishRecoveryFailed(ulong attemptCount, string message)
    {
        PublishStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.RecoveryFailed,
            CanStatusSeverity.Error,
            sequence: Hub.AllocateSequence(),
            channelIndex: Key.ChannelIndex,
            count: attemptCount,
            message: message));
    }

    private static CanRecoveryTrigger MapRecoveryTrigger(CanStatusEvent statusEvent)
    {
        var trigger = CanRecoveryTrigger.None;

        if (statusEvent.Code == CanStatusCode.BusOff)
            trigger |= CanRecoveryTrigger.BusOff;
        if (statusEvent.Kind == CanStatusKind.Bus &&
            statusEvent.Code == CanStatusCode.NativeDriverEvent &&
            statusEvent.Severity >= CanStatusSeverity.Warning)
        {
            trigger |= CanRecoveryTrigger.ErrorPassive;
        }

        if (statusEvent.Kind is CanStatusKind.Bus or CanStatusKind.Receive &&
            statusEvent.Code == CanStatusCode.NativeDriverError)
        {
            trigger |= CanRecoveryTrigger.NativeReceiveFault;
        }

        if (statusEvent.Kind == CanStatusKind.Transmit &&
            statusEvent.Code == CanStatusCode.NativeDriverError)
        {
            trigger |= CanRecoveryTrigger.NativeTransmitFault;
        }

        return trigger;
    }

    private static bool IsRecoveryOpenException(Exception ex) =>
        ex is CanException or ObjectDisposedException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;

    private bool RejectTransmitsWhileRecovering
    {
        get
        {
            lock (_statusGate)
                return _recovery.RejectTransmitsWhileRecovering;
        }
    }

    private static TimeSpan NextBackoffDelay(TimeSpan current, TimeSpan max)
    {
        if (current <= TimeSpan.Zero || max <= TimeSpan.Zero)
            return TimeSpan.Zero;

        var nextTicks = Math.Min(current.Ticks * 2, max.Ticks);
        return TimeSpan.FromTicks(nextTicks);
    }

    private static async Task DelayIfNeededAsync(TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay).ConfigureAwait(false);
    }

    private static void InvokeStatusHandler(Action<CanStatusEvent> handler, CanStatusEvent statusEvent)
    {
        try
        {
            handler(statusEvent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"CanHub.Vector status handler failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
