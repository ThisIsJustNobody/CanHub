using CanHub.Core;

namespace CanHub.Adapter.Virtual.Internal;

/// <summary>
/// 虚拟通道状态。维护通道级别的引用计数和帧广播 Hub。<br/>
/// Virtual channel state. Maintains channel-level reference counting and a frame broadcast hub.
/// </summary>
internal sealed class VirtualChannelState
{
    private readonly object _statusGate = new();
    private readonly object _injectorGate = new();
    private readonly HashSet<VirtualFaultInjector> _faultInjectors = [];
    private int _referenceCount;
    private int _isOpen = 1;
    private int _disposed;
    private int _recoveryInProgress;
    private CanRecoveryOptions _recovery = CanRecoveryOptions.Disabled;
    private event Action<CanStatusEvent>? _statusChanged;

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

    /// <summary>通道当前是否打开。<br/>Whether the channel is currently open.</summary>
    public bool IsOpen => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _isOpen) != 0;

    /// <summary>通道是否正在自动恢复。<br/>Whether the channel is currently recovering.</summary>
    public bool IsRecovering => Volatile.Read(ref _recoveryInProgress) != 0;

    /// <summary>当前是否允许提交发送。<br/>Whether transmit submission is currently allowed.</summary>
    public bool CanSubmitTransmit =>
        IsOpen &&
        (!IsRecovering || !RejectTransmitsWhileRecovering);

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

    /// <summary>配置通道恢复策略。<br/>Configures the channel recovery policy.</summary>
    public void ConfigureRecovery(CanRecoveryOptions recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        lock (_statusGate)
        {
            if (_recovery == CanRecoveryOptions.Disabled || recovery.Mode != CanRecoveryMode.Disabled)
                _recovery = recovery;
        }
    }

    /// <summary>注册故障注入器，并在通道释放时自动注销。<br/>Registers a fault injector and unregisters it when the channel is disposed.</summary>
    public void RegisterFaultInjector(VirtualFaultInjector faultInjector)
    {
        ArgumentNullException.ThrowIfNull(faultInjector);

        lock (_injectorGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            if (_faultInjectors.Add(faultInjector))
                faultInjector.Register(this);
        }
    }

    /// <summary>通道状态变更事件。<br/>Channel status change event.</summary>
    public event Action<CanStatusEvent>? StatusChanged
    {
        add
        {
            if (value is null) return;
            lock (_statusGate)
                _statusChanged += value;
        }
        remove
        {
            if (value is null) return;
            lock (_statusGate)
                _statusChanged -= value;
        }
    }

    /// <summary>注入 bus-off 故障。<br/>Injects a bus-off fault.</summary>
    public void InjectBusOff()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        PublishStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.BusOff,
            CanStatusSeverity.Critical,
            sequence: Hub.AllocateSequence(),
            channelIndex: ChannelIndex,
            message: "Virtual CAN bus-off injected."));

        CanRecoveryOptions recovery;
        lock (_statusGate)
            recovery = _recovery;

        if (recovery.Mode == CanRecoveryMode.Disabled ||
            (recovery.Triggers & CanRecoveryTrigger.BusOff) == CanRecoveryTrigger.None)
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
                channelIndex: ChannelIndex,
                message: "Virtual recovery is already running."));
            return;
        }

        _ = Task.Run(() => RecoverAsync(recovery));
    }

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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Volatile.Write(ref _isOpen, 0);
        Volatile.Write(ref _recoveryInProgress, 0);
        UnregisterFaultInjectors();
        Hub.Dispose();
    }

    private async Task RecoverAsync(CanRecoveryOptions recovery)
    {
        try
        {
            await DelayIfNeededAsync(recovery.FaultDwellTime).ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) != 0)
                return;

            PublishStatus(CanStatusEvent.Create(
                CanStatusKind.Bus,
                CanStatusCode.Recovering,
                CanStatusSeverity.Warning,
                sequence: Hub.AllocateSequence(),
                channelIndex: ChannelIndex,
                message: $"Virtual recovery started: {recovery.Mode}."));

            switch (recovery.Mode)
            {
                case CanRecoveryMode.CloseOnFault:
                    Volatile.Write(ref _isOpen, 0);
                    PublishStatus(CanStatusEvent.Create(
                        CanStatusKind.Channel,
                        CanStatusCode.Disconnected,
                        CanStatusSeverity.Warning,
                        sequence: Hub.AllocateSequence(),
                        channelIndex: ChannelIndex,
                        message: "Virtual channel closed after bus-off."));
                    break;

                case CanRecoveryMode.ResetOnFault:
                    await ReopenOnceAsync(recovery).ConfigureAwait(false);
                    break;

                case CanRecoveryMode.ReopenWithBackoff:
                    await ReopenWithBackoffAsync(recovery).ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            if (IsOpen)
                Volatile.Write(ref _recoveryInProgress, 0);
        }
    }

    private async Task ReopenOnceAsync(CanRecoveryOptions recovery)
    {
        Volatile.Write(ref _isOpen, 0);
        await DelayIfNeededAsync(recovery.RestartDelay).ConfigureAwait(false);
        if (Volatile.Read(ref _disposed) != 0)
            return;

        Volatile.Write(ref _isOpen, 1);
        Volatile.Write(ref _recoveryInProgress, 0);
        PublishStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.Recovered,
            CanStatusSeverity.Info,
            sequence: Hub.AllocateSequence(),
            channelIndex: ChannelIndex,
            message: "Virtual channel reopened after bus-off."));
    }

    private async Task ReopenWithBackoffAsync(CanRecoveryOptions recovery)
    {
        Volatile.Write(ref _isOpen, 0);
        await DelayIfNeededAsync(recovery.RestartDelay).ConfigureAwait(false);
        if (Volatile.Read(ref _disposed) != 0)
            return;

        Volatile.Write(ref _isOpen, 1);
        Volatile.Write(ref _recoveryInProgress, 0);
        PublishStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.Recovered,
            CanStatusSeverity.Info,
            sequence: Hub.AllocateSequence(),
            channelIndex: ChannelIndex,
            count: 1,
            message: "Virtual channel reopened after bus-off."));
    }

    private void PublishStatus(CanStatusEvent statusEvent)
    {
        Delegate[]? handlers;
        lock (_statusGate)
            handlers = _statusChanged?.GetInvocationList();

        if (handlers is null)
            return;

        foreach (Action<CanStatusEvent> handler in handlers)
        {
            try
            {
                handler(statusEvent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"CanHub.Virtual status handler failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private void UnregisterFaultInjectors()
    {
        VirtualFaultInjector[] injectors;
        lock (_injectorGate)
        {
            injectors = _faultInjectors.ToArray();
            _faultInjectors.Clear();
        }

        foreach (var injector in injectors)
            injector.Unregister(this);
    }

    private bool RejectTransmitsWhileRecovering
    {
        get
        {
            lock (_statusGate)
                return _recovery.RejectTransmitsWhileRecovering;
        }
    }

    private static async Task DelayIfNeededAsync(TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay).ConfigureAwait(false);
    }
}
