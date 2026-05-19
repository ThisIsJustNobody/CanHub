using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CanHub.Core;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 异步接收循环。基于通知事件高效等待帧到达。<br/>
/// Vector asynchronous receive loop. Efficiently waits for frame arrival using notification events.
/// </summary>
internal sealed class VectorReceiveLoop
{
    private readonly VectorChannelPort _port;
    private readonly FrameBroadcastHub _hub;
    private readonly Action<CanStatusEvent> _publishStatus;
    private readonly VectorTransmitCorrelationTracker _correlationTracker;
    private readonly object _transmitCorrelationLock = new();
    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private int _started;
    private int _stopRequested;

    public VectorReceiveLoop(
        VectorChannelPort port,
        FrameBroadcastHub hub,
        Action<CanStatusEvent> publishStatus,
        bool transmitEchoEnabled)
    {
        _port = port;
        _hub = hub;
        _publishStatus = publishStatus;
        _correlationTracker = new VectorTransmitCorrelationTracker(transmitEchoEnabled);
    }

    /// <summary>
    /// 启动接收循环。仅允许启动一次，重复调用无操作。<br/>
    /// Starts the receive loop. Only one start is allowed; subsequent calls are no-ops.
    /// </summary>
    public void Start(bool isFd)
    {
        if (Volatile.Read(ref _stopRequested) != 0)
            return;
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        lock (_lifecycleGate)
        {
            if (Volatile.Read(ref _stopRequested) != 0)
                return;

            var cts = new CancellationTokenSource();
            _cts = cts;
            _loopTask = Task.Run(() => RunLoop(isFd, cts.Token));
        }
    }

    public XLDefine.XL_Status TransmitCanFd(
        ulong correlationId, ref uint sent, XLClass.XLcanTxEvent txEvent)
    {
        lock (_transmitCorrelationLock)
        {
            var status = _port.CanTransmitEx(ref sent, txEvent);
            if (status == XLDefine.XL_Status.XL_SUCCESS)
                _correlationTracker.RecordSuccessfulTransmit(correlationId);
            return status;
        }
    }

    /// <summary>
    /// 发送经典 CAN 帧并记录 correlationId，用于 TX 回显时关联发送结果。
    /// 与 TransmitCanFd 对称，统一通过本类进行发送以避免竞争。<br/>
    /// Transmits a classic CAN frame and records the correlationId for TX echo association.
    /// Symmetric with TransmitCanFd; all transmits go through this class to avoid races.
    /// </summary>
    public XLDefine.XL_Status TransmitClassic(ulong correlationId, XLClass.xl_event ev)
    {
        lock (_transmitCorrelationLock)
        {
            var status = _port.CanTransmit(ev);
            if (status == XLDefine.XL_Status.XL_SUCCESS)
                _correlationTracker.RecordSuccessfulTransmit(correlationId);
            return status;
        }
    }

    public bool Stop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
        {
            var existing = Volatile.Read(ref _loopTask);
            return existing is null || existing.IsCompleted;
        }

        CancellationTokenSource? cts;
        Task? task;
        lock (_lifecycleGate)
        {
            cts = _cts;
            task = _loopTask;
        }

        cts?.Cancel();
        if (task is null)
        {
            cts?.Dispose();
            return true;
        }

        try
        {
            if (!task.Wait(TimeSpan.FromSeconds(2)))
            {
                PublishReceiveLoopStatus(
                    CanStatusSeverity.Critical,
                    "Vector receive loop did not stop within 2 seconds; native port close was skipped to avoid use-after-free.");
                return false;
            }
        }
        catch (AggregateException ex) when (AllExceptionsAreCancellation(ex))
        {
        }
        catch (Exception ex)
        {
            PublishReceiveLoopStatus(
                CanStatusSeverity.Warning,
                $"Vector receive loop stopped with an exception: {ex.GetType().Name}: {ex.Message}");
        }

        cts?.Dispose();
        return true;
    }

    private void RunLoop(bool isFd, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_port.NotificationEventHandle != IntPtr.Zero)
                {
                    var waitResult = NativeMethods.WaitForSingleObject(_port.NotificationEventHandle, 100);
                    if (waitResult == NativeMethods.WAIT_FAILED)
                    {
                        var errorCode = Marshal.GetLastPInvokeError();
                        PublishReceiveLoopStatus(
                            CanStatusSeverity.Error,
                            $"Vector receive notification wait failed with Win32 error {errorCode}.",
                            nativeErrorCode: (uint)errorCode);
                        break;
                    }
                }

                if (isFd)
                    ReceiveFdFrames();
                else
                    ReceiveClassicFrames();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                PublishReceiveLoopStatus(
                    CanStatusSeverity.Error,
                    $"Vector receive loop exception: {ex.GetType().Name}: {ex.Message}");
                if (ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(50)))
                    break;
            }
        }
    }

    private static bool AllExceptionsAreCancellation(AggregateException ex) =>
        ex.Flatten().InnerExceptions.All(static item => item is OperationCanceledException);

    private void PublishReceiveLoopStatus(
        CanStatusSeverity severity,
        string message,
        uint nativeErrorCode = 0)
    {
        try
        {
            _publishStatus(CanStatusEvent.Create(
                CanStatusKind.Receive,
                CanStatusCode.NativeDriverError,
                severity,
                channelIndex: _port.LogicalChannelIndex,
                nativeErrorCode: nativeErrorCode,
                message: message));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"CanHub.Vector failed to publish receive-loop status: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ReceiveClassicFrames()
    {
        // 文档要求排空队列直到 XL_ERR_QUEUE_IS_EMPTY
        var xlEvent = new XLClass.xl_event();
        int consecutiveEmpty = 0;
        while (consecutiveEmpty < 3)
        {
            var status = _port.ReceiveClassic(ref xlEvent);
            if (status == XLDefine.XL_Status.XL_SUCCESS)
            {
                consecutiveEmpty = 0;
                ProcessClassicEvent(xlEvent);
            }
            else if (status == XLDefine.XL_Status.XL_ERR_QUEUE_IS_EMPTY)
            {
                consecutiveEmpty++;
            }
            else
            {
                PublishReceiveErrorStatus(status);
                break;
            }
        }
    }

    /// <summary>
    /// 处理经典 CAN 事件。参考 CanConnector.ProcessRawData 的分类逻辑。<br/>
    /// Processes a classic CAN event. Classification logic references CanConnector.ProcessRawData.
    /// </summary>
    private void ProcessClassicEvent(XLClass.xl_event ev)
    {
        var sequence = _hub.AllocateSequence();

        switch (ev.tag)
        {
            case XLDefine.XL_EventTags.XL_RECEIVE_MSG:
                var flags = ev.tagData.can_Msg.flags;

                if (flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_TX_COMPLETED))
                {
                    // 发送成功回显（由 XL_CanSetChannelMode(tx=1) 控制）
                    var correlationId = DequeueCorrelationId();
                    var frameEvent = VectorFrameConverter.FromXlEvent(
                        ev, _port.LogicalChannelIndex, sequence, correlationId);
                    _hub.Broadcast(frameEvent);
                }
                else if (flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_ERROR_FRAME))
                {
                    // 错误帧走正常帧广播路径，携带 ErrorResponse 标志
                    var errorFrameEvent = VectorFrameConverter.FromXlErrorEvent(
                        ev, _port.LogicalChannelIndex, sequence);
                    _hub.Broadcast(errorFrameEvent);
                    PublishErrorFrameStatus(errorFrameEvent, "Classic CAN error frame");
                }
                else
                {
                    // 正常接收帧
                    var frameEvent = VectorFrameConverter.FromXlEvent(
                        ev, _port.LogicalChannelIndex, sequence);
                    _hub.Broadcast(frameEvent);
                }
                break;

            case XLDefine.XL_EventTags.XL_TRANSMIT_MSG:
                // 发送失败反馈（驱动级，并非总线 ACK 失败）
                _publishStatus(CanStatusEvent.Create(
                    CanStatusKind.Transmit, CanStatusCode.NativeDriverError,
                    CanStatusSeverity.Error, sequence: sequence,
                    channelIndex: _port.LogicalChannelIndex,
                    nativeStatusCode: (uint)ev.tag,
                    message: $"Classic CAN transmit failed: id=0x{ev.tagData.can_Msg.id:X}."));
                break;

            case XLDefine.XL_EventTags.XL_CHIP_STATE:
                var chipState = ev.tagData.chipState;
                _publishStatus(BuildChipStateStatus(chipState, _port.LogicalChannelIndex, sequence));
                break;

            case XLDefine.XL_EventTags.XL_SYNC_PULSE:
                _publishStatus(CanStatusEvent.Create(
                    CanStatusKind.Driver, CanStatusCode.NativeDriverEvent,
                    CanStatusSeverity.Info, sequence: sequence,
                    channelIndex: _port.LogicalChannelIndex,
                    message: $"Classic CAN sync pulse."));
                break;

            case XLDefine.XL_EventTags.XL_TRANSCEIVER:
                // 透传器状态变化，暂仅记录
                break;

            default:
                _publishStatus(CanStatusEvent.Create(
                    CanStatusKind.Driver, CanStatusCode.NativeDriverEvent,
                    CanStatusSeverity.Warning, sequence: sequence,
                    channelIndex: _port.LogicalChannelIndex,
                    nativeStatusCode: (uint)ev.tag,
                    message: $"Unhandled classic event: tag={ev.tag}."));
                break;
        }
    }

    private static CanStatusEvent BuildChipStateStatus(
        XLClass.xl_chip_state chipState,
        int channelIndex,
        ulong sequence)
    {
        var busStatus = chipState.busStatus;
        CanStatusCode code;
        CanStatusSeverity severity;

        if (busStatus.HasFlag(XLDefine.XL_BusStatus.XL_CHIPSTAT_BUSOFF))
        {
            code = CanStatusCode.BusOff;
            severity = CanStatusSeverity.Critical;
        }
        else if (busStatus.HasFlag(XLDefine.XL_BusStatus.XL_CHIPSTAT_ERROR_ACTIVE))
        {
            code = CanStatusCode.BusRecovered;
            severity = CanStatusSeverity.Info;
        }
        else if (busStatus.HasFlag(XLDefine.XL_BusStatus.XL_CHIPSTAT_ERROR_PASSIVE) ||
                 busStatus.HasFlag(XLDefine.XL_BusStatus.XL_CHIPSTAT_ERROR_WARNING))
        {
            code = CanStatusCode.NativeDriverEvent;
            severity = CanStatusSeverity.Warning;
        }
        else
        {
            code = CanStatusCode.NativeDriverEvent;
            severity = CanStatusSeverity.Info;
        }

        return CanStatusEvent.Create(
            CanStatusKind.Bus, code, severity, sequence: sequence,
            channelIndex: channelIndex,
            nativeStatusCode: (uint)chipState.busStatus,
            message: $"Classic CAN chip state: busStatus={chipState.busStatus}, " +
                     $"txErrorCounter={chipState.txErrorCounter}, rxErrorCounter={chipState.rxErrorCounter}.");
    }

    /// <summary>
    /// 从发送队列取回一个 correlationId（用于 TX 回显关联）。<br/>
    /// Dequeues a correlationId from the transmit queue (for TX echo association).
    /// </summary>
    private ulong DequeueCorrelationId()
    {
        lock (_transmitCorrelationLock)
        {
            return _correlationTracker.Resolve(userHandle: 0);
        }
    }

    private void ReceiveFdFrames()
    {
        // FD 模式下所有事件（含经典 CAN 帧 echo）均通过 XL_CanReceive 返回，
        // 不应再调用 XL_Receive（经典 CAN 接口）。
        var rxEvent = new XLClass.XLcanRxEvent();
        int consecutiveEmpty = 0;

        while (consecutiveEmpty < 3)
        {
            var status = _port.CanReceive(ref rxEvent);
            if (status == XLDefine.XL_Status.XL_SUCCESS)
            {
                consecutiveEmpty = 0;
                var sequence = _hub.AllocateSequence();
                if (VectorFrameConverter.IsCanFdFrameEvent(rxEvent.tag))
                {
                    var correlationId = ResolveTransmitCorrelationId(rxEvent);
                    var frameEvent = VectorFrameConverter.FromCanFdRxEvent(
                        rxEvent, _port.LogicalChannelIndex, sequence, correlationId);
                    _hub.Broadcast(frameEvent);
                }
                else if (rxEvent.tag == XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_RX_ERROR)
                {
                    // 错误帧走正常帧广播路径，携带 ErrorResponse 标志
                    var errorFrameEvent = VectorFrameConverter.FromCanFdErrorEvent(
                        rxEvent, _port.LogicalChannelIndex, sequence);
                    _hub.Broadcast(errorFrameEvent);
                    PublishErrorFrameStatus(errorFrameEvent, "CAN FD RX_ERROR");
                }
                else
                {
                    var statusEvent = VectorFrameConverter.FromCanFdStatusEvent(rxEvent, _port.LogicalChannelIndex, sequence);
                    _publishStatus(statusEvent);
                }
            }
            else if (status == XLDefine.XL_Status.XL_ERR_QUEUE_IS_EMPTY)
            {
                consecutiveEmpty++;
            }
            else
            {
                PublishReceiveErrorStatus(status);
                break;
            }
        }
    }

    private ulong ResolveTransmitCorrelationId(XLClass.XLcanRxEvent rxEvent)
    {
        if (rxEvent.tag is not (XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_OK
            or XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_REQUEST))
        {
            return 0;
        }

        if (rxEvent.userHandle != 0)
            return rxEvent.userHandle;

        lock (_transmitCorrelationLock)
        {
            return _correlationTracker.Resolve(userHandle: 0);
        }
    }

    private void PublishReceiveErrorStatus(XLDefine.XL_Status status)
    {
        var sequence = _hub.AllocateSequence();
        _publishStatus(CanStatusEvent.Create(
            CanStatusKind.Receive,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            sequence: sequence,
            channelIndex: _port.LogicalChannelIndex,
            nativeStatusCode: (uint)status,
            message: $"Vector receive failed: status={status}."));
    }

    private void PublishErrorFrameStatus(CanFrameEvent frameEvent, string message)
    {
        _publishStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            sequence: frameEvent.Sequence,
            channelIndex: _port.LogicalChannelIndex,
            relatedFrameSequence: frameEvent.Sequence,
            nativeStatusCode: frameEvent.NativeStatusCode,
            nativeErrorCode: frameEvent.NativeErrorCode,
            message: $"{message}: nativeErrorCode=0x{frameEvent.NativeErrorCode:X}."));
    }
}

/// <summary>
/// Win32 原生方法。<br/>
/// Win32 native methods.
/// </summary>
internal static partial class NativeMethods
{
    public const uint WAIT_OBJECT_0 = 0;
    public const uint WAIT_TIMEOUT = 0x102;
    public const uint WAIT_FAILED = 0xFFFFFFFF;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
}

/// <summary>
/// Vector 发送关联追踪器。维护发送帧的 correlationId 队列，用于 TX 回显时匹配发送请求。<br/>
/// Vector transmit correlation tracker. Maintains a correlationId queue for transmit frames,
/// used to match TX echo events to their original transmit requests.
/// </summary>
internal sealed class VectorTransmitCorrelationTracker
{
    /// <summary>最大待处理发送 ID 数量。</summary>
    public const int MaxPendingTransmitIds = 65_536;

    private readonly bool _enabled;
    private readonly ConcurrentQueue<ulong> _pendingTransmitIds = new();
    private int _pendingCount;

    /// <summary>
    /// 创建追踪器。<br/>
    /// Creates a tracker.
    /// </summary>
    public VectorTransmitCorrelationTracker(bool enabled)
    {
        _enabled = enabled;
    }

    /// <summary>当前待处理的发送 ID 数量。</summary>
    public int PendingCount => Volatile.Read(ref _pendingCount);

    /// <summary>
    /// 记录一次成功的发送，将其 correlationId 入队。超过上限时丢弃最旧的条目。<br/>
    /// Records a successful transmit by enqueuing its correlationId. Drops the oldest entry
    /// when the limit is exceeded.
    /// </summary>
    public void RecordSuccessfulTransmit(ulong correlationId)
    {
        if (!_enabled)
            return;

        _pendingTransmitIds.Enqueue(correlationId);
        var count = Interlocked.Increment(ref _pendingCount);

        while (count > MaxPendingTransmitIds)
        {
            if (!_pendingTransmitIds.TryDequeue(out _))
            {
                Interlocked.Exchange(ref _pendingCount, 0);
                break;
            }

            count = Interlocked.Decrement(ref _pendingCount);
        }
    }

    /// <summary>
    /// 解析 correlationId。若 userHandle 非零则直接返回；否则从队列中取出最早的成功发送 ID。<br/>
    /// Resolves the correlationId. Returns userHandle directly if non-zero; otherwise
    /// dequeues the earliest successful transmit ID.
    /// </summary>
    public ulong Resolve(ulong userHandle)
    {
        if (userHandle != 0)
            return userHandle;

        if (!_enabled)
            return 0;

        if (_pendingTransmitIds.TryDequeue(out var id))
        {
            Interlocked.Decrement(ref _pendingCount);
            return id;
        }

        return 0;
    }
}
