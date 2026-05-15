using System.Collections.Concurrent;

namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 设备租约条目。管理单个 ZLG 物理设备的生命周期，支持合并接收循环和通道引用计数。<br/>
/// ZLG device lease entry. Manages the lifecycle of a single ZLG physical device, supporting merged receive loop and channel reference counting.
/// </summary>
internal sealed class ZlgDeviceLeaseEntry : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, ZlgChannelLeaseEntry> _routes = new();
    private readonly ZlgMergedReceiveLoop? _mergedLoop;
    private int _channelReferenceCount;
    private int _disposed;
    private nint _deviceHandle;

    private ZlgDeviceLeaseEntry(
        ZlgDeviceKey key,
        ZlgDeviceCapabilities capabilities,
        nint deviceHandle,
        ZlgDeviceInfo deviceInfo,
        bool mergedReceive)
    {
        Key = key;
        Capabilities = capabilities;
        _deviceHandle = deviceHandle;
        DeviceInfo = deviceInfo;
        MergedReceive = mergedReceive;
        _mergedLoop = mergedReceive ? new ZlgMergedReceiveLoop(this) : null;
    }

    /// <summary>设备键。<br/>Device key.</summary>
    public ZlgDeviceKey Key { get; }

    /// <summary>设备能力。<br/>Device capabilities.</summary>
    public ZlgDeviceCapabilities Capabilities { get; }

    /// <summary>设备句柄。<br/>Device handle.</summary>
    public nint DeviceHandle => Volatile.Read(ref _deviceHandle);

    /// <summary>设备信息。<br/>Device information.</summary>
    public ZlgDeviceInfo DeviceInfo { get; }

    /// <summary>是否使用合并接收模式。<br/>Whether merged receive mode is used.</summary>
    public bool MergedReceive { get; }

    /// <summary>当前引用计数。<br/>Current reference count.</summary>
    public int ReferenceCount => Volatile.Read(ref _channelReferenceCount);

    /// <summary>是否正在关闭或已释放。<br/>Whether closing or disposed.</summary>
    public bool IsClosingOrDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// 打开 ZLG 设备。调用 ZLG 原生 API 获取设备句柄和信息。<br/>
    /// Opens a ZLG device. Calls ZLG native API to obtain device handle and information.
    /// </summary>
    public static ZlgDeviceLeaseEntry Open(
        ZlgDeviceKey key,
        ZlgDeviceCapabilities capabilities,
        bool mergedReceive)
    {
        var handle = ZlgNative.OpenDevice((ZlgDeviceType)key.DeviceTypeId, (uint)key.DeviceIndex);
        try
        {
            var info = ZlgNative.GetDeviceInfo(handle, (uint)key.DeviceIndex, (ZlgDeviceType)key.DeviceTypeId);
            var entry = new ZlgDeviceLeaseEntry(key, capabilities, handle, info, mergedReceive);
            entry.ConfigureDeviceReceiveMerge();
            return entry;
        }
        catch
        {
            var closeStatus = ZlgNative.CloseDevice(handle);
            if (closeStatus != ZlgStatus.Ok)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"CanHub.Zlg failed to close device after open rollback: ZCAN_CloseDevice returned {closeStatus}.");
            }
            throw;
        }
    }

    /// <summary>增加通道引用计数。<br/>Increments the channel reference count.</summary>
    public void AddChannelReference() => Interlocked.Increment(ref _channelReferenceCount);

    /// <summary>减少通道引用计数。<br/>Decrements the channel reference count.</summary>
    public int ReleaseChannelReference() => Interlocked.Decrement(ref _channelReferenceCount);

    /// <summary>标记设备为正在关闭。<br/>Marks the device as closing.</summary>
    public void MarkClosing() => Interlocked.CompareExchange(ref _disposed, 1, 0);

    /// <summary>
    /// 注册通道。将通道条目加入路由表，启动合并接收循环。<br/>
    /// Registers a channel. Adds the channel entry to the route table and starts the merged receive loop.
    /// </summary>
    public void RegisterChannel(ZlgChannelLeaseEntry entry)
    {
        _routes[entry.Key.ChannelIndex] = entry;
        _mergedLoop?.Start();
    }

    /// <summary>
    /// 注销通道。从路由表中移除通道。<br/>
    /// Unregisters a channel. Removes the channel from the route table.
    /// </summary>
    public void UnregisterChannel(int channelIndex)
    {
        _routes.TryRemove(channelIndex, out _);
    }

    /// <summary>
    /// 合并模式发送。将帧转换为 NativeDataObject 并通过设备句柄发送。<br/>
    /// Transmits in merged mode. Converts the frame to NativeDataObject and sends via the device handle.
    /// </summary>
    public uint TransmitMerged(int channelIndex, CanFrame frame, ZlgTransmitType transmitType)
    {
        var handle = DeviceHandle;
        ObjectDisposedException.ThrowIf(handle == 0, this);

        Span<NativeDataObject> buffer = stackalloc NativeDataObject[1];
        buffer[0] = ZlgFrameConverter.ToNativeDataObject(checked((byte)channelIndex), frame, transmitType);
        return ZlgNative.TransmitData(handle, buffer);
    }

    /// <summary>
    /// 分配合并接收数据对象到对应通道。根据通道索引查找通道条目，转换并广播帧事件。<br/>
    /// Dispatches a merged receive data object to the appropriate channel. Looks up the channel entry by index, converts and broadcasts the frame event.
    /// </summary>
    public void DispatchMergedObject(in NativeDataObject obj)
    {
        if (!_routes.TryGetValue(obj.Channel, out var entry))
            return;

        var sequence = entry.HubAllocateSequence();
        var frameEvent = ZlgFrameConverter.FromNativeDataObject(obj, sequence);
        entry.BroadcastFrame(frameEvent);

        if ((ZlgDataObjectType)obj.DataType == ZlgDataObjectType.Error)
            entry.PublishStatus(ZlgFrameConverter.ToStatusEvent(obj, sequence));
    }

    /// <summary>
    /// 向所有已注册通道广播状态事件。<br/>
    /// Publishes a status event to all registered channels.
    /// </summary>
    public void PublishStatusToAll(CanStatusEvent statusEvent)
    {
        foreach (var entry in _routes.Values)
            entry.PublishStatus(statusEvent);
    }

    /// <summary>
    /// 释放设备。停止合并接收循环，关闭设备句柄。<br/>
    /// Disposes the device. Stops the merged receive loop and closes the device handle.
    /// </summary>
    public bool Dispose()
    {
        var state = Volatile.Read(ref _disposed);
        if (state == 2)
            return true;
        Interlocked.CompareExchange(ref _disposed, 1, 0);

        if (_mergedLoop is not null && !_mergedLoop.Stop())
            return false;

        var handle = Interlocked.Exchange(ref _deviceHandle, 0);
        if (handle != 0)
        {
            var status = ZlgNative.CloseDevice(handle);
            if (status != ZlgStatus.Ok)
            {
                PublishStatusToAll(CanStatusEvent.Create(
                    CanStatusKind.Driver,
                    CanStatusCode.NativeDriverError,
                    CanStatusSeverity.Warning,
                    nativeStatusCode: (uint)status,
                    message: $"ZLG close operation failed: ZCAN_CloseDevice returned {status}."));
            }
        }

        Volatile.Write(ref _disposed, 2);
        return true;
    }

    /// <summary>
    /// 异步释放设备。内部调用同步 Dispose。<br/>
    /// Asynchronously disposes the device. Internally calls synchronous Dispose.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ConfigureDeviceReceiveMerge()
    {
        var channelCount = Math.Max(DeviceInfo.CanChannelCount, (byte)Capabilities.DefaultChannelCount);
        for (var channel = 0u; channel < channelCount; channel++)
        {
            ZlgNative.ThrowIfNotOk(
                ZlgNative.SetDeviceReceiveMerge(DeviceHandle, channel, MergedReceive),
                "ZCAN_SetValue(set_device_recv_merge)");
        }
    }
}
