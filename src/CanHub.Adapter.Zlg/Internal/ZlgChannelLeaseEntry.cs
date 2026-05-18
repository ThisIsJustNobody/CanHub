using CanHub.Core;

namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 通道租约条目。管理单个 ZLG CAN 通道的生命周期，包含帧广播中心、发送/接收和状态事件处理。<br/>
/// ZLG channel lease entry. Manages the lifecycle of a single ZLG CAN channel, including frame broadcast hub, send/receive, and status event handling.
/// </summary>
internal sealed class ZlgChannelLeaseEntry : IAsyncDisposable
{
    // 实测 USBCANFD_200U 在 ResetCAN/CloseDevice 返回后，驱动内部状态仍可能短暂残留。
    // 恢复重开时保留一个 ZLG 专属最小等待，避免 restartDelay=0 时撞到 ZCAN_StartCAN Error(0)。
    private static readonly TimeSpan NativeCloseSettleDelay = TimeSpan.FromMilliseconds(500);
    private readonly List<CanStatusEvent> _pendingDiagnostics = [];
    private readonly object _statusGate = new();
    private readonly ZlgChannelOpenSpec _openSpec;
    private readonly IZlgChannelLifecycle _lifecycle;
    private ZlgNonMergedReceiveLoop? _nonMergedLoop;
    private event Action<CanStatusEvent>? _statusChanged;
    private CanRecoveryOptions _recovery;
    private int _referenceCount;
    private int _disposed;
    private int _recoveryInProgress;
    private nint _channelHandle;

    /// <summary>
    /// 创建通道租约条目。<br/>
    /// Creates a channel lease entry.
    /// </summary>
    public ZlgChannelLeaseEntry(
        ZlgChannelKey key,
        ZlgDeviceLeaseEntry device,
        nint channelHandle,
        FrameBroadcastHub hub,
        byte[] fingerprint,
        bool isFd,
        ZlgTransmitType defaultTransmitType,
        string displayName,
        ZlgChannelOpenSpec openSpec,
        CanRecoveryOptions recovery,
        IZlgChannelLifecycle lifecycle)
    {
        ArgumentNullException.ThrowIfNull(openSpec);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(lifecycle);

        Key = key;
        Device = device;
        _channelHandle = channelHandle;
        Hub = hub;
        Fingerprint = fingerprint;
        IsFd = isFd;
        DefaultTransmitType = defaultTransmitType;
        DisplayName = displayName;
        _openSpec = openSpec;
        _recovery = recovery;
        _lifecycle = lifecycle;
        _referenceCount = 1;
    }

    /// <summary>通道键。<br/>Channel key.</summary>
    public ZlgChannelKey Key { get; }

    /// <summary>所属设备租约条目。<br/>Owning device lease entry.</summary>
    public ZlgDeviceLeaseEntry Device { get; }

    /// <summary>通道句柄。<br/>Channel handle.</summary>
    public nint ChannelHandle => Volatile.Read(ref _channelHandle);

    /// <summary>帧广播中心。<br/>Frame broadcast hub.</summary>
    public FrameBroadcastHub Hub { get; }

    /// <summary>配置指纹。<br/>Configuration fingerprint.</summary>
    public byte[] Fingerprint { get; }

    /// <summary>是否为 CAN FD 通道。<br/>Whether this is a CAN FD channel.</summary>
    public bool IsFd { get; }

    /// <summary>默认发送类型。<br/>Default transmit type.</summary>
    public ZlgTransmitType DefaultTransmitType { get; }

    /// <summary>显示名称。<br/>Display name.</summary>
    public string DisplayName { get; }

    /// <summary>通道是否打开。<br/>Whether the channel is open.</summary>
    public bool IsOpen =>
        Volatile.Read(ref _disposed) == 0 &&
        ChannelHandle != 0 &&
        !IsRecovering;

    /// <summary>通道是否正在自动恢复。<br/>Whether the channel is currently recovering.</summary>
    public bool IsRecovering => Volatile.Read(ref _recoveryInProgress) != 0;

    /// <summary>当前是否允许提交发送。<br/>Whether transmit submission is currently allowed.</summary>
    public bool CanSubmitTransmit =>
        Volatile.Read(ref _disposed) == 0 &&
        ChannelHandle != 0 &&
        (!IsRecovering || !RejectTransmitsWhileRecovering);

    /// <summary>是否正在关闭或已释放。<br/>Whether closing or disposed.</summary>
    public bool IsClosingOrDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>当前引用计数。<br/>Current reference count.</summary>
    public int ReferenceCount => Volatile.Read(ref _referenceCount);

    /// <summary>
    /// 状态变更事件。添加处理器时会立即回放缓存的待处理诊断事件。<br/>
    /// Status change event. When a handler is added, cached pending diagnostics are immediately replayed to it.
    /// </summary>
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

    /// <summary>分配发送序列号。<br/>Allocates a transmit sequence number.</summary>
    public ulong HubAllocateSequence() => Hub.AllocateSequence();

    /// <summary>
    /// 尝试增加引用。若通道正在关闭则返回 false。<br/>
    /// Attempts to add a reference. Returns false if the channel is closing.
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

    /// <summary>释放一个引用。<br/>Releases a reference.</summary>
    public int ReleaseReference() => Interlocked.Decrement(ref _referenceCount);

    /// <summary>标记通道为正在关闭。<br/>Marks the channel as closing.</summary>
    public void MarkClosing() => Interlocked.CompareExchange(ref _disposed, 1, 0);

    /// <summary>
    /// 启动接收循环。将通道注册到设备路由表，若非合并模式则启动独立接收循环。<br/>
    /// Starts the receive loop. Registers the channel with the device route table, and starts a non-merged receive loop if applicable.
    /// </summary>
    public void StartReceiveLoop()
    {
        Device.RegisterChannel(this);
        if (!Device.MergedReceive)
            (_nonMergedLoop ??= new ZlgNonMergedReceiveLoop(this)).Start();
    }

    /// <summary>
    /// 发送 CAN 帧。根据设备接收模式选择合并发送或独立通道发送路径。<br/>
    /// Sends a CAN frame. Chooses merged or channel-transmit path based on the device receive mode.
    /// </summary>
    public uint Send(CanFrame frame)
    {
        if (Device.MergedReceive)
            return Device.TransmitMerged(Key.ChannelIndex, frame, DefaultTransmitType);

        if (frame.Flags.HasFlag(CanFrameFlags.FD))
        {
            Span<NativeTransmitFdData> buffer = stackalloc NativeTransmitFdData[1];
            buffer[0] = ZlgFrameConverter.ToNativeTransmitFdData(frame, DefaultTransmitType);
            var handle = ChannelHandle;
            ObjectDisposedException.ThrowIf(handle == 0, this);
            return ZlgNative.TransmitFd(handle, buffer);
        }

        Span<NativeTransmitData> classicBuffer = stackalloc NativeTransmitData[1];
        classicBuffer[0] = ZlgFrameConverter.ToNativeTransmitData(frame, DefaultTransmitType);
        var channelHandle = ChannelHandle;
        ObjectDisposedException.ThrowIf(channelHandle == 0, this);
        return ZlgNative.Transmit(channelHandle, classicBuffer);
    }

    /// <summary>广播帧事件到所有订阅者。<br/>Broadcasts a frame event to all subscribers.</summary>
    public void BroadcastFrame(CanFrameEvent frameEvent) => Hub.Broadcast(frameEvent);

    /// <summary>
    /// 发布状态事件。若无订阅者则缓存为待处理诊断；有新订阅者加入时回放。<br/>
    /// Publishes a status event. Caches as pending diagnostic when no subscribers exist; replays when a new subscriber joins.
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
                sequence: HubAllocateSequence(),
                channelIndex: Key.ChannelIndex,
                message: "ZLG recovery is already running."));
            return;
        }

        _ = Task.Run(() => RecoverAsync(recovery));
    }

    /// <summary>
    /// 处理接收循环原生故障，并按恢复策略决定是否自动恢复。<br/>
    /// Handles a native receive-loop fault and starts automatic recovery when configured.
    /// </summary>
    public void HandleReceiveLoopFault(string message)
    {
        HandleFaultStatus(CanStatusEvent.Create(
            CanStatusKind.Receive,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            sequence: HubAllocateSequence(),
            channelIndex: Key.ChannelIndex,
            message: message));
    }

    /// <summary>
    /// 释放通道。停止非合并接收循环，复位并释放通道句柄。<br/>
    /// Disposes the channel. Stops the non-merged receive loop, resets and releases the channel handle.
    /// </summary>
    public bool Dispose()
    {
        var state = Volatile.Read(ref _disposed);
        if (state == 2)
            return true;
        Interlocked.CompareExchange(ref _disposed, 1, 0);

        if (!StopReceiveLoop())
            return false;

        var handle = Interlocked.Exchange(ref _channelHandle, 0);
        _lifecycle.CloseChannel(handle, Key.ChannelIndex, PublishStatus);

        Hub.Dispose();
        Volatile.Write(ref _disposed, 2);
        return true;
    }

    /// <summary>
    /// 异步释放通道。内部调用同步 Dispose。<br/>
    /// Asynchronously disposes the channel. Internally calls synchronous Dispose.
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
                sequence: HubAllocateSequence(),
                channelIndex: Key.ChannelIndex,
                message: $"ZLG recovery started: {recovery.Mode}."));

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
            if (ChannelHandle != 0 && !IsClosingOrDisposed)
                Volatile.Write(ref _recoveryInProgress, 0);
        }
    }

    private void CloseAfterFault()
    {
        if (CloseChannelForRecovery())
        {
            MarkClosing();
            PublishStatus(CanStatusEvent.Create(
                CanStatusKind.Channel,
                CanStatusCode.Disconnected,
                CanStatusSeverity.Warning,
                sequence: HubAllocateSequence(),
                channelIndex: Key.ChannelIndex,
                message: "ZLG channel closed after bus fault."));
            return;
        }

        PublishRecoveryFailed(0, "ZLG channel close failed during CloseOnFault recovery.");
    }

    private async Task ReopenOnceAsync(CanRecoveryOptions recovery)
    {
        if (!CloseChannelForRecovery())
        {
            PublishRecoveryFailed(1, "ZLG channel close failed before reset reopen.");
            return;
        }

        await DelayIfNeededAsync(EffectiveRestartDelay(recovery.RestartDelay)).ConfigureAwait(false);
        if (IsClosingOrDisposed)
            return;

        try
        {
            OpenChannelAfterRecovery();
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
        if (!CloseChannelForRecovery())
        {
            PublishRecoveryFailed(0, "ZLG channel close failed before backoff reopen.");
            return;
        }

        var attempts = Math.Max(1, recovery.MaxAttempts);
        var delay = recovery.RestartDelay;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await DelayIfNeededAsync(EffectiveRestartDelay(delay)).ConfigureAwait(false);
            if (IsClosingOrDisposed)
                return;

            try
            {
                OpenChannelAfterRecovery();
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
        PublishRecoveryFailed((ulong)attempts, lastException?.Message ?? "ZLG channel reopen failed.");
    }

    private bool CloseChannelForRecovery()
    {
        if (!StopReceiveLoop())
            return false;

        var handle = Interlocked.Exchange(ref _channelHandle, 0);
        return _lifecycle.CloseChannel(handle, Key.ChannelIndex, PublishStatus);
    }

    private void OpenChannelAfterRecovery()
    {
        var handle = _lifecycle.OpenChannel(Device, _openSpec);
        Interlocked.Exchange(ref _channelHandle, handle);
        StartReceiveLoop();
    }

    private bool StopReceiveLoop()
    {
        if (_nonMergedLoop is not null)
        {
            if (!_nonMergedLoop.Stop())
                return false;

            _nonMergedLoop = null;
        }

        Device.UnregisterChannel(Key.ChannelIndex);
        return true;
    }

    private void PublishRecovered(ulong attemptCount)
    {
        if (ChannelHandle != 0 && !IsClosingOrDisposed)
            Volatile.Write(ref _recoveryInProgress, 0);

        PublishStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.Recovered,
            CanStatusSeverity.Info,
            sequence: HubAllocateSequence(),
            channelIndex: Key.ChannelIndex,
            count: attemptCount,
            message: "ZLG channel reopened after bus fault."));
    }

    private void PublishRecoveryFailed(ulong attemptCount, string message)
    {
        PublishStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.RecoveryFailed,
            CanStatusSeverity.Error,
            sequence: HubAllocateSequence(),
            channelIndex: Key.ChannelIndex,
            count: attemptCount,
            message: message));
    }

    private static CanRecoveryTrigger MapRecoveryTrigger(CanStatusEvent statusEvent)
    {
        var trigger = CanRecoveryTrigger.None;
        var nodeState = (ZlgNodeState)((statusEvent.NativeErrorCode >> 8) & 0xFF);

        if (statusEvent.Code == CanStatusCode.BusOff || nodeState == ZlgNodeState.BusOff)
            trigger |= CanRecoveryTrigger.BusOff;
        if (nodeState == ZlgNodeState.Passive)
            trigger |= CanRecoveryTrigger.ErrorPassive;
        if (statusEvent.Kind == CanStatusKind.Bus && statusEvent.Code == CanStatusCode.NativeDriverError)
            trigger |= CanRecoveryTrigger.NativeReceiveFault;
        if (statusEvent.Kind == CanStatusKind.Receive && statusEvent.Code == CanStatusCode.NativeDriverError)
            trigger |= CanRecoveryTrigger.NativeReceiveFault;
        if (statusEvent.Kind == CanStatusKind.Transmit && statusEvent.Code == CanStatusCode.NativeDriverError)
            trigger |= CanRecoveryTrigger.NativeTransmitFault;

        return trigger;
    }

    private static bool IsRecoveryOpenException(Exception ex) =>
        ex is CanException || ex is ObjectDisposedException || ex is ZlgApiException || ZlgExceptionMapper.IsNativeBoundaryException(ex);

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

    private static TimeSpan EffectiveRestartDelay(TimeSpan configuredDelay) =>
        configuredDelay > NativeCloseSettleDelay ? configuredDelay : NativeCloseSettleDelay;

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
                $"CanHub.Zlg status handler failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
