namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 合并接收循环。在后台 Task 中轮询设备数据，将合并数据对象分发到对应通道。<br/>
/// ZLG merged receive loop. Polls device data in a background Task, dispatching merged data objects to the corresponding channels.
/// </summary>
internal sealed class ZlgMergedReceiveLoop
{
    private readonly ZlgDeviceLeaseEntry _device;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lifecycleGate = new();
    private Task? _task;
    private int _started;
    private int _stopped;

    /// <summary>创建合并接收循环。<br/>Creates a merged receive loop.</summary>
    public ZlgMergedReceiveLoop(ZlgDeviceLeaseEntry device)
    {
        _device = device;
    }

    /// <summary>
    /// 启动合并接收循环。线程安全，仅首次调用生效。<br/>
    /// Starts the merged receive loop. Thread-safe; only the first call takes effect.
    /// </summary>
    public void Start()
    {
        if (Volatile.Read(ref _stopped) != 0)
            return;
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        lock (_lifecycleGate)
        {
            if (Volatile.Read(ref _stopped) != 0)
                return;

            _task = Task.Run(RunAsync);
        }
    }

    /// <summary>
    /// 停止合并接收循环。最多等待 2 秒；超时则返回 false。<br/>
    /// Stops the merged receive loop. Waits up to 2 seconds; returns false on timeout.
    /// </summary>
    public bool Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return _task is null || _task.IsCompleted;

        _cts.Cancel();
        Task? task;
        lock (_lifecycleGate)
        {
            task = _task;
        }
        if (task is null)
        {
            _cts.Dispose();
            return true;
        }

        try
        {
            if (!task.Wait(TimeSpan.FromSeconds(2)))
            {
                _device.PublishStatusToAll(CanStatusEvent.Create(
                    CanStatusKind.Receive,
                    CanStatusCode.NativeDriverError,
                    CanStatusSeverity.Critical,
                    message: "ZLG merged receive loop did not stop within 2 seconds; native device close was skipped to avoid use-after-free."));
                return false;
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (AggregateException ex) when (AllExceptionsAreCancellation(ex))
        {
        }
        catch (Exception ex)
        {
            _device.PublishStatusToAll(CreateStopFailureStatus("merged", ex));
        }
        finally
        {
            _cts.Dispose();
        }

        return true;
    }

    private async Task RunAsync()
    {
        var buffer = new NativeDataObject[64];
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var count = ZlgNative.ReceiveData(_device.DeviceHandle, buffer, waitTimeMs: 50);
                for (var i = 0; i < count; i++)
                    _device.DispatchMergedObject(buffer[i]);

                if (count == 0)
                    await Task.Delay(1, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _device.PublishStatusToAll(CanStatusEvent.Create(
                    CanStatusKind.Receive,
                    CanStatusCode.NativeDriverError,
                    CanStatusSeverity.Error,
                    message: $"ZLG merged receive loop failed: {ex.Message}"));
                if (await DelayAfterFaultAsync().ConfigureAwait(false))
                    break;
            }
        }
    }

    private async Task<bool> DelayAfterFaultAsync()
    {
        try
        {
            await Task.Delay(50, _cts.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            return true;
        }
    }

    private static bool AllExceptionsAreCancellation(AggregateException ex) =>
        ex.Flatten().InnerExceptions.All(static item => item is OperationCanceledException);

    private static CanStatusEvent CreateStopFailureStatus(string loopKind, Exception ex) =>
        CanStatusEvent.Create(
            CanStatusKind.Receive,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Warning,
            message: $"ZLG {loopKind} receive loop stopped with an exception: {ex.Message}");
}

/// <summary>
/// ZLG 非合并接收循环。在后台 Task 中分别轮询经典 CAN 和 CAN FD 接收缓冲区。<br/>
/// ZLG non-merged receive loop. Polls classic CAN and CAN FD receive buffers separately in a background Task.
/// </summary>
internal sealed class ZlgNonMergedReceiveLoop
{
    private readonly ZlgChannelLeaseEntry _entry;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lifecycleGate = new();
    private Task? _task;
    private int _started;
    private int _stopped;

    /// <summary>创建非合并接收循环。<br/>Creates a non-merged receive loop.</summary>
    public ZlgNonMergedReceiveLoop(ZlgChannelLeaseEntry entry)
    {
        _entry = entry;
    }

    /// <summary>
    /// 启动非合并接收循环。线程安全，仅首次调用生效。<br/>
    /// Starts the non-merged receive loop. Thread-safe; only the first call takes effect.
    /// </summary>
    public void Start()
    {
        if (Volatile.Read(ref _stopped) != 0)
            return;
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        lock (_lifecycleGate)
        {
            if (Volatile.Read(ref _stopped) != 0)
                return;

            _task = Task.Run(RunAsync);
        }
    }

    /// <summary>
    /// 停止非合并接收循环。最多等待 2 秒；超时则返回 false。<br/>
    /// Stops the non-merged receive loop. Waits up to 2 seconds; returns false on timeout.
    /// </summary>
    public bool Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return _task is null || _task.IsCompleted;

        _cts.Cancel();
        Task? task;
        lock (_lifecycleGate)
        {
            task = _task;
        }
        if (task is null)
        {
            _cts.Dispose();
            return true;
        }

        try
        {
            if (!task.Wait(TimeSpan.FromSeconds(2)))
            {
                _entry.PublishStatus(CanStatusEvent.Create(
                    CanStatusKind.Receive,
                    CanStatusCode.NativeDriverError,
                    CanStatusSeverity.Critical,
                    channelIndex: _entry.Key.ChannelIndex,
                    message: "ZLG non-merged receive loop did not stop within 2 seconds; native channel close was skipped to avoid use-after-free."));
                return false;
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (AggregateException ex) when (AllExceptionsAreCancellation(ex))
        {
        }
        catch (Exception ex)
        {
            _entry.PublishStatus(CreateStopFailureStatus(_entry.Key.ChannelIndex, ex));
        }
        finally
        {
            _cts.Dispose();
        }

        return true;
    }

    private async Task RunAsync()
    {
        var classicBuffer = new NativeReceiveData[64];
        var fdBuffer = new NativeReceiveFdData[64];

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var count = ZlgNative.Receive(_entry.ChannelHandle, classicBuffer, waitTimeMs: 0);
                for (var i = 0; i < count; i++)
                {
                    var sequence = _entry.HubAllocateSequence();
                    _entry.BroadcastFrame(ZlgFrameConverter.FromNativeClassic(classicBuffer[i], _entry.Key.ChannelIndex, sequence));
                }

                var fdCount = ZlgNative.ReceiveFd(_entry.ChannelHandle, fdBuffer, waitTimeMs: 0);
                for (var i = 0; i < fdCount; i++)
                {
                    var sequence = _entry.HubAllocateSequence();
                    _entry.BroadcastFrame(ZlgFrameConverter.FromNativeFd(fdBuffer[i], _entry.Key.ChannelIndex, sequence));
                }

                if (count == 0 && fdCount == 0)
                    await Task.Delay(5, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _entry.PublishStatus(CanStatusEvent.Create(
                    CanStatusKind.Receive,
                    CanStatusCode.NativeDriverError,
                    CanStatusSeverity.Error,
                    channelIndex: _entry.Key.ChannelIndex,
                    message: $"ZLG non-merged receive loop failed: {ex.Message}"));
                if (await DelayAfterFaultAsync().ConfigureAwait(false))
                    break;
            }
        }
    }

    private async Task<bool> DelayAfterFaultAsync()
    {
        try
        {
            await Task.Delay(50, _cts.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            return true;
        }
    }

    private static bool AllExceptionsAreCancellation(AggregateException ex) =>
        ex.Flatten().InnerExceptions.All(static item => item is OperationCanceledException);

    private static CanStatusEvent CreateStopFailureStatus(int channelIndex, Exception ex) =>
        CanStatusEvent.Create(
            CanStatusKind.Receive,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Warning,
            channelIndex: channelIndex,
            message: $"ZLG non-merged receive loop stopped with an exception: {ex.Message}");
}
