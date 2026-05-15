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
    private int _referenceCount;
    private int _disposeState;

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
        string displayName)
    {
        Key = key;
        Driver = driver;
        Port = port;
        Hub = hub;
        ReceiveLoop = new VectorReceiveLoop(port, hub, PublishStatus, port.TransmitEchoEnabled);
        Fingerprint = fingerprint;
        IsFd = isFd;
        DisplayName = displayName;
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
    public VectorReceiveLoop ReceiveLoop { get; }
    /// <summary>配置指纹，用于冲突检测。</summary>
    public byte[] Fingerprint { get; }
    /// <summary>是否为 CAN FD 通道。</summary>
    public bool IsFd { get; }
    /// <summary>会话显示名称。</summary>
    public string DisplayName { get; }
    /// <summary>当前引用计数。</summary>
    public int ReferenceCount => Volatile.Read(ref _referenceCount);
    /// <summary>端口是否已打开。</summary>
    public bool IsOpen => Port.IsOpen;
    /// <summary>是否正在关闭或已释放。</summary>
    public bool IsClosingOrDisposed => Volatile.Read(ref _disposeState) != 0;

    private readonly List<CanStatusEvent> _pendingDiagnostics = [];
    private readonly object _statusGate = new();
    private event Action<CanStatusEvent>? _statusChanged;

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

    /// <summary>递减引用计数。</summary>
    public int ReleaseReference() => Interlocked.Decrement(ref _referenceCount);

    /// <summary>标记条目为关闭中状态。</summary>
    public void MarkClosing() => Interlocked.CompareExchange(ref _disposeState, 1, 0);

    /// <summary>启动接收循环。</summary>
    public void StartReceiveLoop() => ReceiveLoop.Start(IsFd);

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
