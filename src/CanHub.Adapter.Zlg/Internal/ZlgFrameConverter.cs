namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 帧转换器。在 CanHub 托管帧和 ZLG 原生帧结构之间进行转换。<br/>
/// ZLG frame converter. Converts between CanHub managed frames and ZLG native frame structures.
/// </summary>
internal static class ZlgFrameConverter
{
    private const uint IdMask = (uint)ZlgCanIdFlags.IdMask;
    private const uint ExtendedFlag = (uint)ZlgCanIdFlags.Extended;
    private const uint RemoteFlag = (uint)ZlgCanIdFlags.Remote;
    private const uint ErrorFlag = (uint)ZlgCanIdFlags.Error;
    private const long TimestampFrequency = 1_000_000;

    /// <summary>
    /// 将 CanFrame 转换为 NativeTransmitData（经典 CAN 发送）<br/>
    /// Converts a CanFrame to NativeTransmitData for classic CAN transmission.
    /// </summary>
    public static NativeTransmitData ToNativeTransmitData(CanFrame frame, ZlgTransmitType transmitType) =>
        new()
        {
            Frame = ToNativeClassic(frame),
            TransmitType = (uint)transmitType,
        };

    /// <summary>
    /// 将 CanFrame 转换为 NativeTransmitFdData（CAN FD 发送）<br/>
    /// Converts a CanFrame to NativeTransmitFdData for CAN FD transmission.
    /// </summary>
    public static NativeTransmitFdData ToNativeTransmitFdData(CanFrame frame, ZlgTransmitType transmitType) =>
        new()
        {
            Frame = ToNativeFdFrame(frame),
            TransmitType = (uint)transmitType,
        };

    /// <summary>
    /// 将 CanFrame 转换为 NativeDataObject（合并接收模式发送）<br/>
    /// Converts a CanFrame to NativeDataObject for merged receive mode transmission.
    /// </summary>
    public static NativeDataObject ToNativeDataObject(byte channel, CanFrame frame, ZlgTransmitType transmitType)
    {
        var flag = frame.Flags.HasFlag(CanFrameFlags.FD) ? 1u : 0u;
        flag |= ((uint)transmitType & 0x0F) << 4;

        var fdData = new NativeCanFdData
        {
            Flag = flag,
            Frame = ToNativeFdFrame(frame),
        };

        var obj = new NativeDataObject
        {
            DataType = (byte)ZlgDataObjectType.CanOrCanFd,
            Channel = channel,
        };
        obj.WriteCanFdData(fdData);
        return obj;
    }

    /// <summary>
    /// 从 NativeReceiveData 转换为 CanFrameEvent（经典 CAN 接收）<br/>
    /// Converts from NativeReceiveData to CanFrameEvent for classic CAN reception.
    /// </summary>
    public static CanFrameEvent FromNativeClassic(in NativeReceiveData data, int channelIndex, ulong sequence)
    {
        var frame = FromNativeClassicFrame(data.Frame);
        return CanFrameEvent.CreateReceived(
            frame,
            sequence,
            channelIndex: channelIndex,
            deviceTimestampTicks: checked((long)data.Timestamp),
            deviceTimestampFrequency: TimestampFrequency,
            deviceTimestampKind: CanTimestampKind.Relative);
    }

    /// <summary>
    /// 从 NativeReceiveFdData 转换为 CanFrameEvent（CAN FD 接收）<br/>
    /// Converts from NativeReceiveFdData to CanFrameEvent for CAN FD reception.
    /// </summary>
    public static CanFrameEvent FromNativeFd(in NativeReceiveFdData data, int channelIndex, ulong sequence)
    {
        var frame = FromNativeFdFrame(data.Frame, isFd: true);
        return CanFrameEvent.CreateReceived(
            frame,
            sequence,
            channelIndex: channelIndex,
            deviceTimestampTicks: checked((long)data.Timestamp),
            deviceTimestampFrequency: TimestampFrequency,
            deviceTimestampKind: CanTimestampKind.Relative);
    }

    /// <summary>
    /// 从 NativeDataObject 转换为 CanFrameEvent（合并接收模式）<br/>
    /// Converts from NativeDataObject to CanFrameEvent for merged receive mode.
    /// </summary>
    public static CanFrameEvent FromNativeDataObject(in NativeDataObject obj, ulong sequence)
    {
        var type = (ZlgDataObjectType)obj.DataType;
        return type switch
        {
            ZlgDataObjectType.CanOrCanFd => FromMergedCanData(obj, sequence),
            ZlgDataObjectType.Error => FromMergedErrorData(obj, sequence),
            _ => CanFrameEvent.CreateReceived(
                CanFrame.CreateError((uint)obj.DataType),
                sequence,
                channelIndex: obj.Channel,
                eventFlags: CanFrameEventFlags.ErrorResponse,
                nativeStatusCode: obj.DataType,
                nativeErrorCode: obj.DataType),
        };
    }

    /// <summary>
    /// 从 NativeDataObject 转换为 CanStatusEvent（合并接收模式错误状态）<br/>
    /// Converts from NativeDataObject to CanStatusEvent for merged receive mode error status.
    /// </summary>
    public static CanStatusEvent ToStatusEvent(in NativeDataObject obj, ulong sequence)
    {
        var error = obj.ReadErrorData();
        var nativeErrorCode = BuildErrorCode(error);
        var nodeState = (ZlgNodeState)error.NodeState;
        return CanStatusEvent.Create(
            CanStatusKind.Bus,
            MapStatusCode(nodeState),
            MapSeverity(nodeState),
            sequence: sequence,
            channelIndex: obj.Channel,
            nativeStatusCode: error.ErrorType,
            nativeErrorCode: nativeErrorCode,
            message: $"ZLG error: type={(ZlgErrorType)error.ErrorType}, subType={error.ErrorSubType}, node={nodeState}, rx={error.RxErrorCount}, tx={error.TxErrorCount}, data={error.ErrorData}.");
    }

    private static NativeCanFrame ToNativeClassic(CanFrame frame)
    {
        if (frame.Flags.HasFlag(CanFrameFlags.FD))
            throw new ArgumentException("CAN FD frame cannot be sent through classic ZCAN_Transmit.", nameof(frame));

        var native = new NativeCanFrame
        {
            CanId = PackCanId(frame),
            Length = frame.Kind == CanFrameKind.Remote ? frame.Dlc : (byte)frame.Length,
        };
        if (frame.Kind != CanFrameKind.Remote)
        {
            Span<byte> payload = stackalloc byte[frame.Length];
            frame.CopyPayloadTo(payload);
            native.SetData(payload);
        }
        return native;
    }

    private static NativeCanFdFrame ToNativeFdFrame(CanFrame frame)
    {
        var flags = ZlgCanFdFrameFlags.None;
        if (frame.Flags.HasFlag(CanFrameFlags.BRS))
            flags |= ZlgCanFdFrameFlags.BitRateSwitch;
        if (frame.Flags.HasFlag(CanFrameFlags.ESI))
            flags |= ZlgCanFdFrameFlags.ErrorStateIndicator;

        var native = new NativeCanFdFrame
        {
            CanId = PackCanId(frame),
            Flags = (byte)flags,
            Length = frame.Kind == CanFrameKind.Remote ? frame.Dlc : (byte)frame.Length,
        };
        if (frame.Kind != CanFrameKind.Remote)
        {
            Span<byte> payload = stackalloc byte[frame.Length];
            frame.CopyPayloadTo(payload);
            native.SetData(payload);
        }
        return native;
    }

    private static CanFrameEvent FromMergedCanData(in NativeDataObject obj, ulong sequence)
    {
        var data = obj.ReadCanFdData();
        var isFd = (data.Flag & 0x03u) == 1u;
        var txEchoed = (data.Flag & (1u << 9)) != 0;
        var frame = FromNativeFdFrame(data.Frame, isFd);
        return CanFrameEvent.CreateReceived(
            frame,
            sequence,
            channelIndex: obj.Channel,
            deviceTimestampTicks: checked((long)data.Timestamp),
            deviceTimestampFrequency: TimestampFrequency,
            deviceTimestampKind: CanTimestampKind.Relative,
            eventFlags: txEchoed ? CanFrameEventFlags.Loopback : CanFrameEventFlags.None,
            observationKind: txEchoed ? CanFrameObservationKind.DriverEcho : CanFrameObservationKind.Bus,
            nativeStatusCode: obj.DataType);
    }

    private static CanFrameEvent FromMergedErrorData(in NativeDataObject obj, ulong sequence)
    {
        var error = obj.ReadErrorData();
        Span<byte> payload =
        [
            error.ErrorType,
            error.ErrorSubType,
            error.NodeState,
            error.RxErrorCount,
            error.TxErrorCount,
            error.ErrorData,
        ];
        var nativeErrorCode = BuildErrorCode(error);
        return CanFrameEvent.CreateReceived(
            CanFrame.CreateError(nativeErrorCode, payload),
            sequence,
            channelIndex: obj.Channel,
            deviceTimestampTicks: checked((long)error.Timestamp),
            deviceTimestampFrequency: TimestampFrequency,
            deviceTimestampKind: CanTimestampKind.Relative,
            eventFlags: CanFrameEventFlags.ErrorResponse,
            nativeStatusCode: obj.DataType,
            nativeErrorCode: nativeErrorCode);
    }

    private static CanFrame FromNativeClassicFrame(in NativeCanFrame native)
    {
        var isExtended = (native.CanId & ExtendedFlag) != 0;
        var isRemote = (native.CanId & RemoteFlag) != 0;
        var isError = (native.CanId & ErrorFlag) != 0;
        var id = isExtended ? CanId.Extended(native.CanId & IdMask) : CanId.Standard(native.CanId & IdMask);

        if (isError)
        {
            Span<byte> payload = stackalloc byte[native.DataLength];
            native.CopyDataTo(payload);
            return CanFrame.CreateError(native.CanId & IdMask, payload);
        }

        if (isRemote)
            return CanFrame.CreateRemote(id, native.Length);

        Span<byte> data = stackalloc byte[native.DataLength];
        native.CopyDataTo(data);
        return CanFrame.CreateData(id, data);
    }

    private static CanFrame FromNativeFdFrame(in NativeCanFdFrame native, bool isFd)
    {
        var isExtended = (native.CanId & ExtendedFlag) != 0;
        var isRemote = (native.CanId & RemoteFlag) != 0;
        var isError = (native.CanId & ErrorFlag) != 0;
        var id = isExtended ? CanId.Extended(native.CanId & IdMask) : CanId.Standard(native.CanId & IdMask);

        if (isError)
        {
            Span<byte> errorPayload = stackalloc byte[native.DataLength];
            native.CopyDataTo(errorPayload);
            return CanFrame.CreateError(native.CanId & IdMask, errorPayload[..Math.Min(errorPayload.Length, CanFrame.MaxClassicPayloadLength)]);
        }

        if (isRemote)
            return CanFrame.CreateRemote(id, native.Length);

        if (!isFd)
        {
            Span<byte> classicPayload = stackalloc byte[native.DataLength];
            native.CopyDataTo(classicPayload);
            return CanFrame.CreateData(id, classicPayload[..Math.Min(classicPayload.Length, CanFrame.MaxClassicPayloadLength)]);
        }

        var flags = (ZlgCanFdFrameFlags)native.Flags;
        Span<byte> payload = stackalloc byte[native.DataLength];
        native.CopyDataTo(payload);
        return CanFrame.CreateFdData(
            id,
            payload,
            bitRateSwitch: flags.HasFlag(ZlgCanFdFrameFlags.BitRateSwitch),
            errorStateIndicator: flags.HasFlag(ZlgCanFdFrameFlags.ErrorStateIndicator));
    }

    private static uint PackCanId(CanFrame frame)
    {
        var canId = frame.Id.Value & IdMask;
        if (frame.Id.IsExtended)
            canId |= ExtendedFlag;
        if (frame.Kind == CanFrameKind.Remote)
            canId |= RemoteFlag;
        if (frame.Kind == CanFrameKind.Error)
            canId |= ErrorFlag;
        return canId;
    }

    private static uint BuildErrorCode(in NativeErrorData error) =>
        ((uint)error.ErrorType << 24) |
        ((uint)error.ErrorSubType << 16) |
        ((uint)error.NodeState << 8) |
        error.ErrorData;

    private static CanStatusCode MapStatusCode(ZlgNodeState state) =>
        state == ZlgNodeState.BusOff
            ? CanStatusCode.BusOff
            : CanStatusCode.NativeDriverError;

    private static CanStatusSeverity MapSeverity(ZlgNodeState state) =>
        state switch
        {
            ZlgNodeState.BusOff => CanStatusSeverity.Critical,
            ZlgNodeState.Passive => CanStatusSeverity.Error,
            ZlgNodeState.Warning => CanStatusSeverity.Warning,
            _ => CanStatusSeverity.Error,
        };
}
