using CanHub.Core;

namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 通道租约条目。管理单个 ZLG CAN 通道的生命周期，包含帧广播中心、发送/接收和状态事件处理。<br/>
/// ZLG channel lease entry. Manages the lifecycle of a single ZLG CAN channel, including frame broadcast hub, send/receive, and status event handling.
/// </summary>
internal sealed class ZlgChannelLeaseEntry : IAsyncDisposable
{
    private readonly List<CanStatusEvent> _pendingDiagnostics = [];
    private readonly object _statusGate = new();
    private readonly ZlgNonMergedReceiveLoop? _nonMergedLoop;
    private event Action<CanStatusEvent>? _statusChanged;
    private int _referenceCount;
    private int _disposed;
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
        string displayName)
    {
        Key = key;
        Device = device;
        _channelHandle = channelHandle;
        Hub = hub;
        Fingerprint = fingerprint;
        IsFd = isFd;
        DefaultTransmitType = defaultTransmitType;
        DisplayName = displayName;
        _referenceCount = 1;
        _nonMergedLoop = device.MergedReceive ? null : new ZlgNonMergedReceiveLoop(this);
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
    public bool IsOpen => Volatile.Read(ref _disposed) == 0 && ChannelHandle != 0;

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
        _nonMergedLoop?.Start();
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
    /// 释放通道。停止非合并接收循环，复位并释放通道句柄。<br/>
    /// Disposes the channel. Stops the non-merged receive loop, resets and releases the channel handle.
    /// </summary>
    public bool Dispose()
    {
        var state = Volatile.Read(ref _disposed);
        if (state == 2)
            return true;
        Interlocked.CompareExchange(ref _disposed, 1, 0);

        if (_nonMergedLoop is not null && !_nonMergedLoop.Stop())
            return false;
        Device.UnregisterChannel(Key.ChannelIndex);

        var handle = Interlocked.Exchange(ref _channelHandle, 0);
        if (handle != 0)
        {
            var status = ZlgNative.ResetCan(handle);
            if (status != ZlgStatus.Ok)
            {
                PublishStatus(CanStatusEvent.Create(
                    CanStatusKind.Driver,
                    CanStatusCode.NativeDriverError,
                    CanStatusSeverity.Warning,
                    channelIndex: Key.ChannelIndex,
                    nativeStatusCode: (uint)status,
                    message: $"ZLG close operation failed: ZCAN_ResetCAN returned {status}."));
            }
        }

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
