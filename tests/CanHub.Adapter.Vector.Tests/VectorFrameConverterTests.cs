using CanHub.Adapter.Vector.Internal;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorFrameConverterTests
{
    [TestMethod]
    public void CorrelationTracker_Disabled_DoesNotRecordPendingIds()
    {
        var tracker = new VectorTransmitCorrelationTracker(enabled: false);

        tracker.RecordSuccessfulTransmit(1);

        Assert.AreEqual(0, tracker.PendingCount);
        Assert.AreEqual(0ul, tracker.Resolve(userHandle: 0));
        Assert.AreEqual(123ul, tracker.Resolve(userHandle: 123));
    }

    [TestMethod]
    public void CorrelationTracker_Enabled_RecordsAndResolvesPendingIds()
    {
        var tracker = new VectorTransmitCorrelationTracker(enabled: true);

        tracker.RecordSuccessfulTransmit(10);
        tracker.RecordSuccessfulTransmit(11);

        Assert.AreEqual(2, tracker.PendingCount);
        Assert.AreEqual(10ul, tracker.Resolve(userHandle: 0));
        Assert.AreEqual(11ul, tracker.Resolve(userHandle: 0));
        Assert.AreEqual(0, tracker.PendingCount);
    }

    [TestMethod]
    public void CorrelationTracker_UserHandleDoesNotConsumePendingId()
    {
        var tracker = new VectorTransmitCorrelationTracker(enabled: true);
        tracker.RecordSuccessfulTransmit(10);

        Assert.AreEqual(99ul, tracker.Resolve(userHandle: 99));

        Assert.AreEqual(1, tracker.PendingCount);
        Assert.AreEqual(10ul, tracker.Resolve(userHandle: 0));
    }

    [TestMethod]
    public void CorrelationTracker_ExcessPendingIds_DropsOldest()
    {
        var tracker = new VectorTransmitCorrelationTracker(enabled: true);

        for (var i = 1; i <= VectorTransmitCorrelationTracker.MaxPendingTransmitIds + 10; i++)
            tracker.RecordSuccessfulTransmit((ulong)i);

        Assert.AreEqual(VectorTransmitCorrelationTracker.MaxPendingTransmitIds, tracker.PendingCount);
        Assert.AreEqual(11ul, tracker.Resolve(userHandle: 0));
    }

    [TestMethod]
    public void ToXlEvent_StandardData()
    {
        var frame = CanFrame.CreateData(CanId.Standard(0x123), [0x01, 0x02, 0x03]);
        var ev = VectorFrameConverter.ToXlEvent(frame);
        Assert.AreEqual(XLDefine.XL_EventTags.XL_TRANSMIT_MSG, ev.tag);
        Assert.AreEqual(0x123u, ev.tagData.can_Msg.id);
        Assert.AreEqual((ushort)3, ev.tagData.can_Msg.dlc);
        Assert.AreEqual(0x01, ev.tagData.can_Msg.data[0]);
    }

    [TestMethod]
    public void ToXlEvent_ExtendedData()
    {
        var frame = CanFrame.CreateData(CanId.Extended(0x1FFFFFFF), [0xAA]);
        var ev = VectorFrameConverter.ToXlEvent(frame);
        // Extended ID is encoded in bit 31 of the id field
        Assert.AreEqual(0x9FFFFFFFu, ev.tagData.can_Msg.id);
    }

    [TestMethod]
    public void ToXlEvent_RemoteFrame()
    {
        var frame = CanFrame.CreateRemote(CanId.Standard(0x200), dlc: 6);
        var ev = VectorFrameConverter.ToXlEvent(frame);
        Assert.AreEqual(0x200u, ev.tagData.can_Msg.id);
        Assert.AreEqual((ushort)6, ev.tagData.can_Msg.dlc);
        Assert.IsTrue(ev.tagData.can_Msg.flags.HasFlag(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_REMOTE_FRAME));
    }

    [TestMethod]
    public void ToCanFdTxEvent_StandardFd()
    {
        var payload = new byte[64];
        payload[0] = 0xBB;
        var frame = CanFrame.CreateFdData(CanId.Standard(0x300), payload);
        var tx = VectorFrameConverter.ToCanFdTxEvent(frame);
        Assert.AreEqual(XLDefine.XL_CANFD_TX_EventTags.XL_CAN_EV_TAG_TX_MSG, tx.tag);
        Assert.AreEqual(0x300u, tx.tagData.canId);
        Assert.AreEqual(0xBB, tx.tagData.data[0]);
        Assert.IsTrue(tx.tagData.msgFlags.HasFlag(XLDefine.XL_CANFD_TX_MessageFlags.XL_CAN_TXMSG_FLAG_EDL));
        Assert.IsTrue(tx.tagData.msgFlags.HasFlag(XLDefine.XL_CANFD_TX_MessageFlags.XL_CAN_TXMSG_FLAG_BRS));
    }

    [TestMethod]
    public void ToCanFdTxEvent_RemoteFrame_PreservesDlc()
    {
        var frame = CanFrame.CreateRemote(CanId.Standard(0x301), dlc: 5);

        var tx = VectorFrameConverter.ToCanFdTxEvent(frame);

        Assert.AreEqual(XLDefine.XL_CANFD_DLC.DLC_CAN_CANFD_5_BYTES, tx.tagData.dlc);
        Assert.IsTrue(tx.tagData.msgFlags.HasFlag(XLDefine.XL_CANFD_TX_MessageFlags.XL_CAN_TXMSG_FLAG_RTR));
    }

    [TestMethod]
    public void FromXlEvent_StandardData()
    {
        var ev = new XLClass.xl_event
        {
            tag = XLDefine.XL_EventTags.XL_RECEIVE_MSG,
            tagData = new XLClass.xl_tag_data
            {
                can_Msg = new XLClass.xl_can_msg
                {
                    id = 0x123,
                    dlc = 2,
                    data = [0x01, 0x02, 0, 0, 0, 0, 0, 0],
                    flags = XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NONE,
                }
            }
        };
        var frameEvent = VectorFrameConverter.FromXlEvent(ev, channelIndex: 0, sequence: 1);
        Assert.AreEqual(0x123u, frameEvent.Frame.Id.Value);
        Assert.IsFalse(frameEvent.Frame.Id.IsExtended);
        Assert.AreEqual(2, frameEvent.Frame.Length);
        Assert.AreEqual(0x01, frameEvent.Frame.GetPayloadByte(0));
    }

    [TestMethod]
    public void FromCanFdRxEvent_StandardFd()
    {
        var data = new byte[64];
        data[0] = 0xCC;
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_RX_OK,
            tagData = new XLClass.XL_CAN_TAG_DATA
            {
                canRxOkMsg = new XLClass.XL_CAN_EV_RX_MSG
                {
                    canId = 0x400,
                    msgFlags = XLDefine.XL_CANFD_RX_MessageFlags.XL_CAN_RXMSG_FLAG_EDL,
                    dlc = XLDefine.XL_CANFD_DLC.DLC_CAN_CANFD_1_BYTES,
                    data = data,
                }
            }
        };
        var frameEvent = VectorFrameConverter.FromCanFdRxEvent(rx, channelIndex: 1, sequence: 42);
        Assert.AreEqual(0x400u, frameEvent.Frame.Id.Value);
        Assert.IsTrue(frameEvent.Frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.AreEqual(1, frameEvent.Frame.Length);
    }

    [TestMethod]
    public void FromCanFdRxEvent_TxOk_ProducesTransmitConfirmation()
    {
        var data = new byte[64];
        data[0] = 0xDD;
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_OK,
            userHandle = 7,
            tagData = new XLClass.XL_CAN_TAG_DATA
            {
                canTxOkMsg = new XLClass.XL_CAN_EV_RX_MSG
                {
                    canId = 0x401,
                    msgFlags = XLDefine.XL_CANFD_RX_MessageFlags.XL_CAN_RXMSG_FLAG_EDL,
                    dlc = XLDefine.XL_CANFD_DLC.DLC_CAN_CANFD_1_BYTES,
                    data = data,
                }
            }
        };

        var frameEvent = VectorFrameConverter.FromCanFdRxEvent(rx, channelIndex: 1, sequence: 43);

        Assert.AreEqual(CanFrameDirection.Transmit, frameEvent.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxConfirmed, frameEvent.ObservationKind);
        Assert.AreEqual(CanTransmitOutcome.Transmitted, frameEvent.Outcome);
        Assert.AreEqual(7ul, frameEvent.CorrelationId);
        Assert.AreEqual(0x401u, frameEvent.Frame.Id.Value);
    }

    [TestMethod]
    public void FromCanFdRxEvent_TxOk_UsesCorrelationOverride()
    {
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_OK,
            userHandle = 0,
            tagData = new XLClass.XL_CAN_TAG_DATA
            {
                canTxOkMsg = new XLClass.XL_CAN_EV_RX_MSG
                {
                    canId = 0x401,
                    msgFlags = XLDefine.XL_CANFD_RX_MessageFlags.XL_CAN_RXMSG_FLAG_EDL,
                    dlc = XLDefine.XL_CANFD_DLC.DLC_CAN_CANFD_0_BYTES,
                    data = new byte[64],
                }
            }
        };

        var frameEvent = VectorFrameConverter.FromCanFdRxEvent(
            rx, channelIndex: 1, sequence: 43, correlationId: 123);

        Assert.AreEqual(123ul, frameEvent.CorrelationId);
    }

    [TestMethod]
    public void FromCanFdRxEvent_TxRequest_ProducesTransmitSubmission()
    {
        var data = new byte[64];
        data[0] = 0xEE;
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_TX_REQUEST,
            userHandle = 8,
            tagData = new XLClass.XL_CAN_TAG_DATA
            {
                canTxRequest = new XLClass.XL_CAN_EV_TX_REQUEST
                {
                    canId = 0x402,
                    msgFlags = XLDefine.XL_CANFD_RX_MessageFlags.XL_CAN_RXMSG_FLAG_EDL,
                    dlc = XLDefine.XL_CANFD_DLC.DLC_CAN_CANFD_1_BYTES,
                    data = data,
                }
            }
        };

        var frameEvent = VectorFrameConverter.FromCanFdRxEvent(rx, channelIndex: 1, sequence: 44);

        Assert.AreEqual(CanFrameDirection.Transmit, frameEvent.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxSubmit, frameEvent.ObservationKind);
        Assert.AreEqual(8ul, frameEvent.CorrelationId);
        Assert.AreEqual(0x402u, frameEvent.Frame.Id.Value);
        Assert.AreEqual(0xEE, frameEvent.Frame.GetPayloadByte(0));
    }

    [TestMethod]
    public void FromCanFdRxEvent_NonFrameTag_Throws()
    {
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_RX_ERROR,
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => VectorFrameConverter.FromCanFdRxEvent(rx, channelIndex: 0, sequence: 1));
    }

    [TestMethod]
    public void FromCanFdStatusEvent_RxError_FallsThroughToDefault()
    {
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_RX_ERROR,
            tagData = new XLClass.XL_CAN_TAG_DATA
            {
                canError = new XLClass.XL_CAN_EV_ERROR
                {
                    errorCode = 0x12,
                }
            }
        };

        // RX_ERROR is now handled by FromCanFdErrorEvent (frame path), not FromCanFdStatusEvent.
        // Falls through to default → NativeDriverEvent with Warning severity.
        var statusEvent = VectorFrameConverter.FromCanFdStatusEvent(rx, channelIndex: 2, sequence: 45);

        Assert.AreEqual(CanStatusKind.Driver, statusEvent.Kind);
        Assert.AreEqual(CanStatusCode.NativeDriverEvent, statusEvent.Code);
        Assert.AreEqual(CanStatusSeverity.Warning, statusEvent.Severity);
    }

    [TestMethod]
    public void FromCanFdStatusEvent_ChipStateBusOff_ProducesBusOffStatus()
    {
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_CHIP_STATE,
            tagData = new XLClass.XL_CAN_TAG_DATA
            {
                canChipState = new XLClass.XL_CAN_EV_CHIP_STATE
                {
                    busStatus = XLDefine.XL_BusStatus.XL_CHIPSTAT_BUSOFF,
                    txErrorCounter = 10,
                    rxErrorCounter = 20,
                }
            }
        };

        var statusEvent = VectorFrameConverter.FromCanFdStatusEvent(rx, channelIndex: 2, sequence: 46);

        Assert.AreEqual(CanStatusKind.Bus, statusEvent.Kind);
        Assert.AreEqual(CanStatusCode.BusOff, statusEvent.Code);
        Assert.AreEqual(CanStatusSeverity.Critical, statusEvent.Severity);
        Assert.AreEqual((uint)XLDefine.XL_BusStatus.XL_CHIPSTAT_BUSOFF, statusEvent.NativeStatusCode);
        StringAssert.Contains(statusEvent.Message, "txErrorCounter=10");
    }

    [TestMethod]
    public void FromCanFdStatusEvent_SyncPulse_ProducesNativeDriverEvent()
    {
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_SYNC_PULSE,
            tagData = new XLClass.XL_CAN_TAG_DATA
            {
                canSyncPulse = new XLClass.XL_CAN_EV_SYNC_PULSE
                {
                    triggerSource = 0xAA,
                    time = 123,
                }
            }
        };

        var statusEvent = VectorFrameConverter.FromCanFdStatusEvent(rx, channelIndex: 2, sequence: 47);

        Assert.AreEqual(CanStatusKind.Driver, statusEvent.Kind);
        Assert.AreEqual(CanStatusCode.NativeDriverEvent, statusEvent.Code);
        Assert.AreEqual(CanStatusSeverity.Info, statusEvent.Severity);
        StringAssert.Contains(statusEvent.Message, "SYNC_PULSE");
    }

    [TestMethod]
    public void FromCanFdErrorEvent_ProducesErrorFrameEvent()
    {
        var rx = new XLClass.XLcanRxEvent
        {
            tag = XLDefine.XL_CANFD_RX_EventTags.XL_CAN_EV_TAG_RX_ERROR,
            tagData = new XLClass.XL_CAN_TAG_DATA
            {
                canError = new XLClass.XL_CAN_EV_ERROR
                {
                    errorCode = 0xD2,
                }
            }
        };

        var frameEvent = VectorFrameConverter.FromCanFdErrorEvent(rx, channelIndex: 2, sequence: 45);

        Assert.AreEqual(CanFrameKind.Error, frameEvent.Frame.Kind);
        Assert.AreEqual(CanFrameDirection.Receive, frameEvent.Direction);
        Assert.IsTrue(frameEvent.EventFlags.HasFlag(CanFrameEventFlags.ErrorResponse));
        Assert.AreEqual(0xD2u, frameEvent.NativeErrorCode);
        Assert.AreEqual(0xD2u, frameEvent.Frame.ErrorCode);
        Assert.AreEqual(2, frameEvent.ChannelIndex);
    }

    [TestMethod]
    public void FromXlErrorEvent_ProducesErrorFrameEvent()
    {
        var ev = new XLClass.xl_event
        {
            tag = XLDefine.XL_EventTags.XL_RECEIVE_MSG,
            tagData = new XLClass.xl_tag_data
            {
                can_Msg = new XLClass.xl_can_msg
                {
                    id = 0x200,
                    flags = XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_ERROR_FRAME
                        | XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NERR,
                    data = [0, 0, 0, 0, 0, 0, 0, 0],
                }
            }
        };

        var frameEvent = VectorFrameConverter.FromXlErrorEvent(ev, channelIndex: 1, sequence: 10);

        Assert.AreEqual(CanFrameKind.Error, frameEvent.Frame.Kind);
        Assert.AreEqual(CanFrameDirection.Receive, frameEvent.Direction);
        Assert.IsTrue(frameEvent.EventFlags.HasFlag(CanFrameEventFlags.ErrorResponse));
        var expectedErrorCode = (uint)(XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_ERROR_FRAME
            | XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_NERR);
        Assert.AreEqual(expectedErrorCode, frameEvent.NativeErrorCode);
        Assert.AreEqual(expectedErrorCode, frameEvent.Frame.ErrorCode);
        Assert.AreEqual(1, frameEvent.ChannelIndex);
    }
}
