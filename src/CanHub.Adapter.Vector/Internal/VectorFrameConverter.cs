using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// CanFrame 与 Vector 原生类型的双向转换。<br/>
/// Bidirectional conversion between CanFrame and Vector native types.
/// </summary>
internal static class VectorFrameConverter
{
    private const uint ExtendedCanIdFlag = 0x80000000u;

    /// <summary>
    /// 将 CanFrame 转换为经典 CAN 发送事件 (xl_event)。<br/>
    /// Converts CanFrame to a classic CAN transmit event (xl_event).
    /// </summary>
    public static XLClass.xl_event ToXlEvent(CanFrame frame)
    {
        var msg = new XLClass.xl_can_msg
        {
            id = frame.Id.Value,
            dlc = frame.Dlc,
            flags = BuildClassicFlags(frame),
        };

        if (frame.Id.IsExtended)
            msg.id |= (uint)XLDefine.XL_MessageFlagsExtended.XL_CAN_EXT_MSG_ID;

        if (frame.Length > 0)
        {
            msg.data = new byte[frame.Length];
            frame.CopyPayloadTo(msg.data);
        }

        return new XLClass.xl_event
        {
            tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG,
            tagData = new XLClass.xl_tag_data { can_Msg = msg },
        };
    }

    /// <summary>
    /// 将 CanFrame 转换为 CAN FD 发送事件 (XLcanTxEvent)。<br/>
    /// Converts CanFrame to a CAN FD transmit event (XLcanTxEvent).
    /// </summary>
    public static XLClass.XLcanTxEvent ToCanFdTxEvent(CanFrame frame, int channelIndex = 0)
    {
        var msgFlags = XLDefine.XL_CANFD_TX_MessageFlags.XL_CAN_TXMSG_FLAG_NONE;
        if (frame.Flags.HasFlag(CanFrameFlags.FD))
            msgFlags |= XLDefine.XL_CANFD_TX_MessageFlags.XL_CAN_TXMSG_FLAG_EDL;
        if (frame.Flags.HasFlag(CanFrameFlags.BRS))
            msgFlags |= XLDefine.XL_CANFD_TX_MessageFlags.XL_CAN_TXMSG_FLAG_BRS;
        if (frame.Kind == CanFrameKind.Remote)
            msgFlags |= XLDefine.XL_CANFD_TX_MessageFlags.XL_CAN_TXMSG_FLAG_RTR;

        var txMsg = new XLClass.XL_CAN_TX_MSG
        {
            canId = frame.Id.Value,
            msgFlags = msgFlags,
            dlc = frame.Kind == CanFrameKind.Remote
                ? (XLDefine.XL_CANFD_DLC)frame.Dlc
                : LengthToCanFdDlc(frame.Length),
        };

        if (frame.Id.IsExtended)
            txMsg.canId |= ExtendedCanIdFlag;

        if (frame.Length > 0)
        {
            txMsg.data = new byte[frame.Length];
            frame.CopyPayloadTo(txMsg.data);
        }

        return new XLClass.XLcanTxEvent
        {
            tag = XLDefine.XL_CANFD_TX_EventTags.XL_CAN_EV_TAG_TX_MSG,
            channelIndex = (byte)channelIndex,
            tagData = txMsg,
        };
    }

    /// <summary>
    /// 将经典 CAN 接收事件 (xl_event) 转换为 CanFrameEvent。
    /// 根据 TX_COMPLETED 标志区分正常接收和发送成功回显。<br/>
    /// Converts a classic CAN receive event (xl_event) to CanFrameEvent.
    /// Distinguishes normal reception from transmit success echo via the TX_COMPLETED flag.
    /// </summary>
    public static CanFrameEvent FromXlEvent(
        XLClass.xl_event ev, int channelIndex, ulong sequence, ulong correlationId = 0)
    {
        var canMsg = ev.tagData.can_Msg;
        var isExtended = (canMsg.id & (uint)XLDefine.XL_MessageFlagsExtended.XL_CAN_EXT_MSG_ID) != 0;
        var isRemote = canMsg.flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_REMOTE_FRAME);
        var canIdValue = canMsg.id & 0x7FFFFFFF;
        var canId = isExtended ? CanId.Extended(canIdValue) : CanId.Standard(canIdValue);
        var dlc = (byte)(canMsg.dlc & 0x0F);
        var dataLength = CanFrame.DlcToLength(dlc);

        CanFrame frame;
        if (isRemote)
        {
            frame = CanFrame.CreateRemote(canId, dlc);
        }
        else
        {
            var payloadSpan = canMsg.data.AsSpan(0, Math.Min(dataLength, canMsg.data.Length));
            frame = CanFrame.CreateData(canId, payloadSpan);
        }

        // TX_COMPLETED 标志 → 发送成功回显（XL_CanSetChannelMode tx=1 时产生）
        if (canMsg.flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_TX_COMPLETED))
        {
            return CanFrameEvent.CreateTransmitted(
                correlationId, frame, CanTransmitOutcome.Transmitted,
                sequence: sequence, channelIndex: channelIndex);
        }

        return CanFrameEvent.CreateReceived(frame, sequence, channelIndex: channelIndex);
    }

    /// <summary>
    /// 判断 CAN FD 接收事件是否能转换为 CanFrameEvent。<br/>
    /// Determines whether a CAN FD receive event can be converted to CanFrameEvent.
    /// </summary>
    public static bool IsCanFdFrameEvent(XLDefine.XL_CANFD_RX_EventTags tag) =>
        tag is XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_RX_OK
            or XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_OK
            or XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_REQUEST;

    /// <summary>
    /// 将 CAN FD RX_ERROR 事件转换为携带错误帧标志的 CanFrameEvent。<br/>
    /// Converts a CAN FD RX_ERROR event to CanFrameEvent with error frame flags.
    /// </summary>
    public static CanFrameEvent FromCanFdErrorEvent(
        XLClass.XLcanRxEvent rx, int channelIndex, ulong sequence)
    {
        var errorCode = rx.tagData.canError.errorCode;
        var frame = CanFrame.CreateError(errorCode);
        return CanFrameEvent.CreateReceived(
            frame,
            sequence,
            channelIndex: channelIndex,
            eventFlags: CanFrameEventFlags.ErrorResponse,
            nativeStatusCode: (uint)rx.tag,
            nativeErrorCode: errorCode);
    }

    /// <summary>
    /// 将经典 CAN 错误帧事件 (XL_RECEIVE_MSG + ERROR_FRAME) 转换为携带错误帧标志的 CanFrameEvent。<br/>
    /// Converts a classic CAN error frame event (XL_RECEIVE_MSG + ERROR_FRAME) to
    /// CanFrameEvent with error frame flags.
    /// </summary>
    public static CanFrameEvent FromXlErrorEvent(
        XLClass.xl_event ev, int channelIndex, ulong sequence)
    {
        var canMsg = ev.tagData.can_Msg;
        var errorCode = (uint)canMsg.flags;
        var frame = CanFrame.CreateError(errorCode);
        return CanFrameEvent.CreateReceived(
            frame,
            sequence,
            channelIndex: channelIndex,
            eventFlags: CanFrameEventFlags.ErrorResponse,
            nativeStatusCode: (uint)ev.tag,
            nativeErrorCode: errorCode);
    }

    /// <summary>
    /// 将 CAN FD 接收事件 (XLcanRxEvent) 中的帧类 tag 转换为 CanFrameEvent。
    /// 非帧 tag 应由接收循环转换为 CanStatusEvent。<br/>
    /// Converts frame-type tags in a CAN FD receive event (XLcanRxEvent) to CanFrameEvent.
    /// Non-frame tags should be converted to CanStatusEvent by the receive loop.
    /// </summary>
    public static CanFrameEvent FromCanFdRxEvent(
        XLClass.XLcanRxEvent rx, int channelIndex, ulong sequence, ulong correlationId = 0) =>
        rx.tag switch
        {
            XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_RX_OK =>
                BuildReceivedFrameEvent(rx.tagData.canRxOkMsg, rx, channelIndex, sequence),

            XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_OK =>
                BuildTransmittedFrameEvent(rx.tagData.canTxOkMsg, rx, channelIndex, sequence, correlationId),

            XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_REQUEST =>
                BuildTransmitRequestFrameEvent(rx.tagData.canTxRequest, rx, channelIndex, sequence, correlationId),

            _ => throw new ArgumentOutOfRangeException(nameof(rx), rx.tag,
                "CAN FD event tag cannot be converted to CanFrameEvent.")
        };

    /// <summary>
    /// 将 CAN FD 接收事件 (XLcanRxEvent) 中的非帧 tag 转换为 CanStatusEvent。<br/>
    /// Converts non-frame tags in a CAN FD receive event (XLcanRxEvent) to CanStatusEvent.
    /// </summary>
    public static CanStatusEvent FromCanFdStatusEvent(XLClass.XLcanRxEvent rx, int channelIndex, ulong sequence) =>
        rx.tag switch
        {
            XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_ERROR =>
                BuildErrorStatus(rx, channelIndex, sequence, CanStatusKind.Transmit, "TX_ERROR"),

            XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_CHIP_STATE =>
                BuildChipStateStatus(rx, channelIndex, sequence),

            XLDefine.XL_CANFD_RX_EventTags.XL_SYNC_PULSE =>
                BuildSyncPulseStatus(rx, channelIndex, sequence),

            _ => CanStatusEvent.Create(
                CanStatusKind.Driver,
                CanStatusCode.NativeDriverEvent,
                CanStatusSeverity.Warning,
                sequence: sequence,
                channelIndex: channelIndex,
                nativeStatusCode: (uint)rx.tag,
                message: $"Vector CAN FD event {rx.tag} is not a frame event.")
        };

    /// <summary>
    /// 从 XL_CAN_EV_RX_MSG 构造 CanFrameEvent。RX_OK 和 TX_OK (echo) 共用相同的消息结构体。<br/>
    /// Builds CanFrameEvent from XL_CAN_EV_RX_MSG. RX_OK and TX_OK (echo) share the same message struct.
    /// </summary>
    private static CanFrameEvent BuildReceivedFrameEvent(
        XLClass.XL_CAN_EV_RX_MSG msg, XLClass.XLcanRxEvent rx, int channelIndex, ulong sequence)
    {
        var frame = BuildFrame(msg.canId, msg.msgFlags, msg.dlc, msg.data);
        return CanFrameEvent.CreateReceived(
            frame,
            sequence,
            channelIndex: channelIndex,
            nativeStatusCode: (uint)rx.tag);
    }

    private static CanFrameEvent BuildTransmittedFrameEvent(
        XLClass.XL_CAN_EV_RX_MSG msg,
        XLClass.XLcanRxEvent rx,
        int channelIndex,
        ulong sequence,
        ulong correlationId)
    {
        var frame = BuildFrame(msg.canId, msg.msgFlags, msg.dlc, msg.data);
        return CanFrameEvent.CreateTransmitted(
            ResolveCorrelationId(rx, correlationId),
            frame,
            CanTransmitOutcome.Transmitted,
            sequence: sequence,
            channelIndex: channelIndex,
            nativeStatusCode: (uint)rx.tag);
    }

    private static CanFrameEvent BuildTransmitRequestFrameEvent(
        XLClass.XL_CAN_EV_TX_REQUEST msg,
        XLClass.XLcanRxEvent rx,
        int channelIndex,
        ulong sequence,
        ulong correlationId)
    {
        var frame = BuildFrame(msg.canId, msg.msgFlags, msg.dlc, msg.data);
        return CanFrameEvent.CreateTransmitSubmission(
            ResolveCorrelationId(rx, correlationId),
            frame,
            accepted: false,
            sequence: sequence,
            channelIndex: channelIndex,
            nativeStatusCode: (uint)rx.tag);
    }

    private static ulong ResolveCorrelationId(XLClass.XLcanRxEvent rx, ulong correlationId) =>
        correlationId != 0 ? correlationId : rx.userHandle;

    private static CanFrame BuildFrame(
        uint nativeCanId,
        XLDefine.XL_CANFD_RX_MessageFlags msgFlags,
        XLDefine.XL_CANFD_DLC nativeDlc,
        byte[]? data)
    {
        var isExtended = (nativeCanId & ExtendedCanIdFlag) != 0;
        var isFd = msgFlags.HasFlag(XLDefine.XL_CANFD_RX_MessageFlags.XL_CAN_RXMSG_FLAG_EDL);
        var isBrs = msgFlags.HasFlag(XLDefine.XL_CANFD_RX_MessageFlags.XL_CAN_RXMSG_FLAG_BRS);
        var isEsi = msgFlags.HasFlag(XLDefine.XL_CANFD_RX_MessageFlags.XL_CAN_RXMSG_FLAG_ESI);
        var isRemote = msgFlags.HasFlag(XLDefine.XL_CANFD_RX_MessageFlags.XL_CAN_RXMSG_FLAG_RTR);
        var canIdValue = nativeCanId & ~ExtendedCanIdFlag;
        var canId = isExtended ? CanId.Extended(canIdValue) : CanId.Standard(canIdValue);
        var dlc = (byte)nativeDlc;
        var dataLength = CanFrame.DlcToLength(dlc);

        if (isRemote)
        {
            return CanFrame.CreateRemote(canId, dlc);
        }

        Span<byte> payload = stackalloc byte[dataLength];
        if (data is not null && data.Length > 0)
        {
            data.AsSpan(0, Math.Min(dataLength, data.Length)).CopyTo(payload);
        }

        return isFd
            ? CanFrame.CreateFdData(canId, payload, bitRateSwitch: isBrs, errorStateIndicator: isEsi)
            : CanFrame.CreateData(canId, payload);
    }

    private static CanStatusEvent BuildErrorStatus(
        XLClass.XLcanRxEvent rx,
        int channelIndex,
        ulong sequence,
        CanStatusKind kind,
        string tagName)
    {
        var error = rx.tagData.canError;
        return CanStatusEvent.Create(
            kind,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            sequence: sequence,
            channelIndex: channelIndex,
            nativeStatusCode: (uint)rx.tag,
            nativeErrorCode: error.errorCode,
            message: $"Vector CAN FD {tagName}: errorCode=0x{error.errorCode:X2}, flagsChip={rx.flagsChip}.");
    }

    private static CanStatusEvent BuildChipStateStatus(
        XLClass.XLcanRxEvent rx, int channelIndex, ulong sequence)
    {
        var chip = rx.tagData.canChipState;
        var (code, severity) = MapChipState(chip.busStatus);

        return CanStatusEvent.Create(
            CanStatusKind.Bus,
            code,
            severity,
            sequence: sequence,
            channelIndex: channelIndex,
            nativeStatusCode: (uint)chip.busStatus,
            message: $"Vector CAN FD CHIP_STATE: busStatus={chip.busStatus}, txErrorCounter={chip.txErrorCounter}, rxErrorCounter={chip.rxErrorCounter}, flagsChip={rx.flagsChip}.");
    }

    private static CanStatusEvent BuildSyncPulseStatus(
        XLClass.XLcanRxEvent rx, int channelIndex, ulong sequence)
    {
        var pulse = rx.tagData.canSyncPulse;
        return CanStatusEvent.Create(
            CanStatusKind.Driver,
            CanStatusCode.NativeDriverEvent,
            CanStatusSeverity.Info,
            sequence: sequence,
            channelIndex: channelIndex,
            nativeStatusCode: (uint)rx.tag,
            message: $"Vector CAN FD SYNC_PULSE: triggerSource=0x{pulse.triggerSource:X}, time={pulse.time}, flagsChip={rx.flagsChip}.");
    }

    private static (CanStatusCode Code, CanStatusSeverity Severity) MapChipState(
        XLDefine.XL_BusStatus busStatus)
    {
        if (busStatus.HasFlag(XLDefine.XL_BusStatus.XL_CHIPSTAT_BUSOFF))
            return (CanStatusCode.BusOff, CanStatusSeverity.Critical);
        if (busStatus.HasFlag(XLDefine.XL_BusStatus.XL_CHIPSTAT_ERROR_ACTIVE))
            return (CanStatusCode.BusRecovered, CanStatusSeverity.Info);
        if (busStatus.HasFlag(XLDefine.XL_BusStatus.XL_CHIPSTAT_ERROR_PASSIVE) ||
            busStatus.HasFlag(XLDefine.XL_BusStatus.XL_CHIPSTAT_ERROR_WARNING))
        {
            return (CanStatusCode.NativeDriverEvent, CanStatusSeverity.Warning);
        }

        return (CanStatusCode.NativeDriverEvent, CanStatusSeverity.Info);
    }

    /// <summary>
    /// 字节长度 → CAN FD DLC 枚举值（0-15）。CAN FD DLC 0-8 直接映射，9-15 映射到 12/16/20/24/32/48/64 字节。<br/>
    /// Maps byte length to CAN FD DLC enum value (0-15). DLC 0-8 map directly;
    /// 9-15 map to 12/16/20/24/32/48/64 bytes.
    /// </summary>
    private static XLDefine.XL_CANFD_DLC LengthToCanFdDlc(int length)
    {
        // CAN FD DLC 映射：字节数 → DLC 码（0-15）
        ReadOnlySpan<int> dlcToBytes = [0, 1, 2, 3, 4, 5, 6, 7, 8, 12, 16, 20, 24, 32, 48, 64];
        for (int dlc = 0; dlc < dlcToBytes.Length; dlc++)
        {
            if (length <= dlcToBytes[dlc])
                return (XLDefine.XL_CANFD_DLC)dlc;
        }
        return (XLDefine.XL_CANFD_DLC)15; // DLC 15 = 64 字节
    }

    private static XLDefine.XL_MessageFlags BuildClassicFlags(CanFrame frame)
    {
        var flags = XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE;
        if (frame.Kind == CanFrameKind.Remote)
            flags |= XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_REMOTE_FRAME;
        return flags;
    }
}
