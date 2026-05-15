using System.Numerics;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 通道端口管理。封装端口的打开、关闭、激活、去激活和帧收发。<br/>
/// Vector channel port management. Encapsulates port open, close, activation, deactivation,
/// and frame transmit/receive.
/// </summary>
internal sealed class VectorChannelPort : IChannelLease
{
    private readonly VectorDriver _driver;
    private int _portHandle = -1;
    private ulong _accessMask;
    private ulong _permissionMask;
    private int _notificationHandle = -1;
    private int _referenceCount;
    private int _disposed;

    /// <summary>逻辑通道索引，由端点解析产生。</summary>
    public int LogicalChannelIndex { get; }
    /// <summary>原生通道索引，由通道掩码计算得到。</summary>
    public int NativeChannelIndex { get; }

    /// <inheritdoc />
    public int ChannelIndex => LogicalChannelIndex;

    /// <summary>通道访问掩码。</summary>
    public ulong ChannelMask { get; }
    /// <inheritdoc />
    public int ReferenceCount => Volatile.Read(ref _referenceCount);
    /// <summary>端口是否已打开。</summary>
    public bool IsOpen => _portHandle >= 0;
    /// <summary>是否为 CAN FD 端口。</summary>
    public bool IsFd { get; private set; }
    /// <summary>是否启用发送回显。</summary>
    public bool TransmitEchoEnabled { get; private set; }
    /// <summary>通知事件句柄，用于高效等待帧到达。</summary>
    public IntPtr NotificationEventHandle => new(_notificationHandle);

    /// <summary>
    /// 初始化 Vector 通道端口。<br/>
    /// Initializes a Vector channel port.
    /// </summary>
    public VectorChannelPort(VectorDriver driver, ulong channelMask, int logicalChannelIndex)
    {
        _driver = driver;
        ChannelMask = channelMask;
        NativeChannelIndex = BitOperations.TrailingZeroCount(channelMask);
        LogicalChannelIndex = logicalChannelIndex;
    }

    /// <summary>
    /// 激活通道端口。首次调用打开端口、配置参数、设置通知并激活通道；后续调用仅增加引用计数。<br/>
    /// Activates the channel port. The first call opens the port, configures parameters,
    /// sets up notification, and activates the channel; subsequent calls only increment
    /// the reference count.
    /// </summary>
    public ValueTask ActivateAsync(CanOpenContext context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Increment(ref _referenceCount) == 1)
        {
            var busParams = context.Options.BusParameters;
            try
            {
                OpenPort(busParams.IsFd, context.Options.NativeOptions);
                ConfigureBusParameters(busParams, context.Options.NativeOptions);
                IsFd = busParams.IsFd;

                SetNotification();
                ActivateChannel();
            }
            catch
            {
                // 回滚：若端口已打开则关闭，还原引用计数
                DeactivateChannel(publishStatus: null);
                ClosePort(publishStatus: null);
                Interlocked.Decrement(ref _referenceCount);
                throw;
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 去激活通道端口。递减引用计数，当引用计数归零时关闭通道和端口。<br/>
    /// Deactivates the channel port. Decrements the reference count, and closes the
    /// channel and port when the reference count reaches zero.
    /// </summary>
    public ValueTask DeactivateAsync(CancellationToken ct = default)
    {
        if (Interlocked.Decrement(ref _referenceCount) <= 0 && IsOpen)
        {
            DeactivateChannel(publishStatus: null);
            ClosePort(publishStatus: null);
        }
        return ValueTask.CompletedTask;
    }

    private void OpenPort(bool isFd, object? nativeOptions)
    {
        var vectorOptions = nativeOptions as VectorOpenOptions;
        var applicationName = string.IsNullOrWhiteSpace(vectorOptions?.ApplicationName)
            ? "CanHub"
            : vectorOptions.ApplicationName;
        var rxQueueSize = vectorOptions?.RxQueueSize > 0
            ? (uint)vectorOptions.RxQueueSize
            : isFd ? 65536u : 256u;

        _accessMask = ChannelMask;
        _permissionMask = _accessMask;
        var interfaceVersion = isFd
            ? XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION_V4
            : XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION;
        var status = VectorDriver.Driver.XL_OpenPort(
            ref _portHandle, applicationName, _accessMask, ref _permissionMask,
            rxQueueSize, interfaceVersion,
            XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
        if (status != XLDefine.XL_Status.XL_SUCCESS)
        {
            var errorStr = VectorDriver.Driver.XL_GetErrorString(status);
            throw new CanException("vector", CanErrorCategory.AdapterError,
                nativeFunction: $"XL_OpenPort(mask=0x{_accessMask:X},error={errorStr})", vendorCode: (int)status);
        }
    }

    private void ConfigureBusParameters(CanBusParameters busParams, object? nativeOptions)
    {
        var vectorOptions = nativeOptions as VectorOpenOptions;
        var ignoreForeignConfiguration = vectorOptions?.IgnoreForeignConfiguration == true;
        ValidateSupportedBitrates(busParams);

        // 1) 时序参数
        if (busParams.IsFd)
        {
            var fdConf = CreateCanFdConfiguration(busParams);
            var status = VectorDriver.Driver.XL_CanFdSetConfiguration(
                _portHandle, _accessMask, fdConf);
            if (status != XLDefine.XL_Status.XL_SUCCESS &&
                !(ignoreForeignConfiguration && status == XLDefine.XL_Status.XL_ERR_INVALID_ACCESS))
            {
                throw new CanException("vector", CanErrorCategory.AdapterError,
                    nativeFunction: "XL_CanFdSetConfiguration", vendorCode: (int)status);
            }
        }
        else
        {
            var chipParams = CreateClassicChipParams(busParams, vectorOptions);
            var status = VectorDriver.Driver.XL_CanSetChannelParams(
                _portHandle, _accessMask, chipParams);
            if (status != XLDefine.XL_Status.XL_SUCCESS &&
                !(ignoreForeignConfiguration && status == XLDefine.XL_Status.XL_ERR_INVALID_ACCESS))
            {
                throw new CanException("vector", CanErrorCategory.AdapterError,
                    nativeFunction: "XL_CanSetChannelParams", vendorCode: (int)status);
            }
        }

        // 2) SetChannelOutput —— 输出模式（正常 / 静默）
        if (busParams.AckOff.HasValue)
        {
            var outputMode = busParams.AckOff.Value
                ? XLDefine.XL_OutputMode.XL_OUTPUT_MODE_SILENT
                : XLDefine.XL_OutputMode.XL_OUTPUT_MODE_NORMAL;
            var outputStatus = VectorDriver.Driver.XL_CanSetChannelOutput(
                _portHandle, _permissionMask, outputMode);
            if (outputStatus != XLDefine.XL_Status.XL_SUCCESS)
            {
                throw new CanException("vector", CanErrorCategory.AdapterError,
                    nativeFunction: "XL_CanSetChannelOutput", vendorCode: (int)outputStatus);
            }
        }

        // 3) Vector 专有选项：SetChannelMode + SetReceiveMode
        TransmitEchoEnabled = false;
        if (vectorOptions is not null)
        {
            var channelModeStatus = VectorDriver.Driver.XL_CanSetChannelMode(
                _portHandle, _accessMask, tx: vectorOptions.TransmitEcho ? 1u : 0u,
                txRq: vectorOptions.ReadyToSendEvent ? 1u : 0u);
            if (channelModeStatus != XLDefine.XL_Status.XL_SUCCESS)
            {
                throw new CanException("vector", CanErrorCategory.AdapterError,
                    nativeFunction: "XL_CanSetChannelMode", vendorCode: (int)channelModeStatus);
            }

            var receiveModeStatus = VectorDriver.Driver.XL_CanSetReceiveMode(
                _portHandle, errorFrame: vectorOptions.SuppressErrorFrames ? (byte)1 : (byte)0,
                chipState: vectorOptions.SuppressChipState ? (byte)1 : (byte)0);
            if (receiveModeStatus != XLDefine.XL_Status.XL_SUCCESS)
            {
                throw new CanException("vector", CanErrorCategory.AdapterError,
                    nativeFunction: "XL_CanSetReceiveMode", vendorCode: (int)receiveModeStatus);
            }

            TransmitEchoEnabled = vectorOptions.TransmitEcho;
        }
    }

    internal static XLClass.XLcanFdConf CreateCanFdConfiguration(CanBusParameters busParams)
    {
        ValidateSupportedBitrates(busParams);

        var dataBitrate = RequireDataBitrate(busParams);
        var (_, arbTseg1, arbTseg2, arbSjw) =
            VectorBusParameterCalculator.CalculateFdArbitrationBitTiming(busParams.ArbitrationBitrate);
        var (_, dataTseg1, dataTseg2, dataSjw) =
            VectorBusParameterCalculator.CalculateFdDataBitTiming(dataBitrate);

        return new XLClass.XLcanFdConf
        {
            arbitrationBitRate = (uint)busParams.ArbitrationBitrate,
            sjwAbr = (uint)(busParams.ArbitrationSjw ?? arbSjw),
            tseg1Abr = (uint)(busParams.ArbitrationTseg1 ?? arbTseg1),
            tseg2Abr = (uint)(busParams.ArbitrationTseg2 ?? arbTseg2),
            dataBitRate = (uint)dataBitrate,
            sjwDbr = (uint)(busParams.DataSjw ?? dataSjw),
            tseg1Dbr = (uint)(busParams.DataTseg1 ?? dataTseg1),
            tseg2Dbr = (uint)(busParams.DataTseg2 ?? dataTseg2),
            options = (byte)(busParams.IsNonIsoFd == true ? 8 : 0),
        };
    }

    internal static XLClass.xl_chip_params CreateClassicChipParams(
        CanBusParameters busParams,
        VectorOpenOptions? vectorOptions)
    {
        ValidateSupportedBitrates(busParams);

        var (_, tseg1, tseg2, sjw) =
            VectorBusParameterCalculator.CalculateClassicBitTiming(busParams.ArbitrationBitrate);

        return new XLClass.xl_chip_params
        {
            bitrate = (uint)busParams.ArbitrationBitrate,
            sjw = ToClassicTimingByte(busParams.ArbitrationSjw ?? sjw, nameof(busParams.ArbitrationSjw), max: 4),
            tseg1 = ToClassicTimingByte(busParams.ArbitrationTseg1 ?? tseg1, nameof(busParams.ArbitrationTseg1), max: 16),
            tseg2 = ToClassicTimingByte(busParams.ArbitrationTseg2 ?? tseg2, nameof(busParams.ArbitrationTseg2), max: 8),
            sam = vectorOptions?.Sam ?? (byte)1,
        };
    }

    private static void ValidateSupportedBitrates(CanBusParameters busParams)
    {
        if (!VectorBusParameterCalculator.IsValidBitrate(busParams.ArbitrationBitrate, isFd: false))
        {
            throw new CanException("vector", CanErrorCategory.ConfigurationConflict,
                $"Vector adapter does not support arbitration bitrate {busParams.ArbitrationBitrate}.");
        }

        if (busParams.IsFd)
        {
            var dataBitrate = RequireDataBitrate(busParams);
            if (!VectorBusParameterCalculator.IsValidBitrate(dataBitrate, isFd: true))
            {
                throw new CanException("vector", CanErrorCategory.ConfigurationConflict,
                    $"Vector adapter does not support CAN FD data bitrate {dataBitrate}.");
            }
        }
    }

    private static int RequireDataBitrate(CanBusParameters busParams) =>
        busParams.DataBitrate
        ?? throw new ArgumentNullException(nameof(busParams.DataBitrate), "CAN FD data bitrate must be specified.");

    private static byte ToClassicTimingByte(int value, string parameterName, int max)
    {
        if (value < 1 || value > max)
        {
            throw new CanException("vector", CanErrorCategory.ConfigurationConflict,
                $"Classic CAN timing parameter '{parameterName}' must be in range 1..{max}, but received {value}.");
        }

        return (byte)value;
    }

    private void ActivateChannel()
    {
        var status = VectorDriver.Driver.XL_ActivateChannel(
            _portHandle, _accessMask, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN,
            XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
        if (status != XLDefine.XL_Status.XL_SUCCESS)
        {
            var errorStr = VectorDriver.Driver.XL_GetErrorString(status);
            throw new CanException("vector", CanErrorCategory.AdapterError,
                nativeFunction: $"XL_ActivateChannel(mask=0x{_accessMask:X},error={errorStr})", vendorCode: (int)status);
        }
    }

    private void SetNotification()
    {
        int handle = -1;
        var status = VectorDriver.Driver.XL_SetNotification(_portHandle, ref handle, 1);
        if (status != XLDefine.XL_Status.XL_SUCCESS)
            throw new CanException("vector", CanErrorCategory.AdapterError,
                nativeFunction: "XL_SetNotification", vendorCode: (int)status);
        _notificationHandle = handle;
    }

    private void DeactivateChannel(Action<CanStatusEvent>? publishStatus)
    {
        if (!IsOpen) return;
        var status = VectorDriver.Driver.XL_DeactivateChannel(_portHandle, _accessMask);
        if (status != XLDefine.XL_Status.XL_SUCCESS)
            publishStatus?.Invoke(CreateCloseStatus("XL_DeactivateChannel", status));
    }

    private void ClosePort(Action<CanStatusEvent>? publishStatus)
    {
        if (!IsOpen) return;
        var status = VectorDriver.Driver.XL_ClosePort(_portHandle);
        if (status != XLDefine.XL_Status.XL_SUCCESS)
            publishStatus?.Invoke(CreateCloseStatus("XL_ClosePort", status));
        _portHandle = -1;
        _notificationHandle = -1;
    }

    internal XLDefine.XL_Status ReceiveClassic(ref XLClass.xl_event ev)
        => VectorDriver.Driver.XL_Receive(_portHandle, ref ev);

    internal XLDefine.XL_Status CanReceive(ref XLClass.XLcanRxEvent rx)
        => VectorDriver.Driver.XL_CanReceive(_portHandle, ref rx);

    internal XLDefine.XL_Status CanTransmit(XLClass.xl_event ev)
        => VectorDriver.Driver.XL_CanTransmit(_portHandle, _accessMask, ev);

    internal XLDefine.XL_Status CanTransmitEx(ref uint sent, XLClass.XLcanTxEvent tx)
        => VectorDriver.Driver.XL_CanTransmitEx(_portHandle, _accessMask, ref sent, tx);

    public void Dispose(Action<CanStatusEvent>? publishStatus = null)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        DeactivateChannel(publishStatus);
        ClosePort(publishStatus);
    }

    /// <summary>
    /// 异步释放端口。去激活通道并关闭端口。<br/>
    /// Asynchronously disposes the port. Deactivates the channel and closes the port.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 异步释放端口，并将关闭状态通过回调发布。<br/>
    /// Asynchronously disposes the port, publishing close status via callback.
    /// </summary>
    public ValueTask DisposeAsync(Action<CanStatusEvent> publishStatus)
    {
        Dispose(publishStatus);
        return ValueTask.CompletedTask;
    }

    private CanStatusEvent CreateCloseStatus(string nativeFunction, XLDefine.XL_Status status) =>
        CanStatusEvent.Create(
            CanStatusKind.Driver,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Warning,
            channelIndex: LogicalChannelIndex,
            nativeStatusCode: (uint)status,
            message: $"Vector close operation failed: {nativeFunction} returned {status} ({VectorDriver.Driver.XL_GetErrorString(status)}).");
}
