using System.Runtime.CompilerServices;
using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg.Tests;

[TestClass]
public sealed class ZlgFrameConverterTests
{
    [TestMethod(DisplayName = "Classic frame converts to ZCAN_Transmit shape")]
    public void ClassicFrame_ConvertsToTransmitData()
    {
        var frame = CanFrame.CreateData(CanId.Standard(0x123), [0x01, 0x02, 0x03]);

        var native = ZlgFrameConverter.ToNativeTransmitData(frame, ZlgTransmitType.Single);

        Assert.AreEqual(0x123u, native.Frame.CanId);
        Assert.AreEqual((byte)3, native.Frame.Length);
        Assert.AreEqual((uint)ZlgTransmitType.Single, native.TransmitType);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, native.Frame.GetData());
    }

    [TestMethod(DisplayName = "Extended remote frame preserves flags and DLC")]
    public void RemoteFrame_PreservesExtendedRemoteFlagsAndDlc()
    {
        var frame = CanFrame.CreateRemote(CanId.Extended(0x1ABCDE), dlc: 6);

        var native = ZlgFrameConverter.ToNativeTransmitData(frame, ZlgTransmitType.Normal);

        Assert.AreEqual(0x1ABCDEu | (uint)ZlgCanIdFlags.Extended | (uint)ZlgCanIdFlags.Remote, native.Frame.CanId);
        Assert.AreEqual((byte)6, native.Frame.Length);
    }

    [TestMethod(DisplayName = "FD frame round-trips through merged data object")]
    public void FdFrame_RoundTripsMergedDataObject()
    {
        var payload = Enumerable.Range(0, 20).Select(static i => (byte)i).ToArray();
        var frame = CanFrame.CreateFdData(CanId.Extended(0x1ABCDE), payload, bitRateSwitch: true);

        var native = ZlgFrameConverter.ToNativeDataObject(channel: 1, frame, ZlgTransmitType.Single);
        var restored = ZlgFrameConverter.FromNativeDataObject(native, sequence: 42);

        Assert.AreEqual(ZlgDataObjectType.CanOrCanFd, (ZlgDataObjectType)native.DataType);
        Assert.AreEqual(1, restored.ChannelIndex);
        Assert.AreEqual(42ul, restored.Sequence);
        Assert.AreEqual(CanFrameDirection.Receive, restored.Direction);
        Assert.AreEqual(CanFrameObservationKind.Bus, restored.ObservationKind);
        Assert.AreEqual(0x1ABCDEu, restored.Frame.Id.Value);
        Assert.IsTrue(restored.Frame.Id.IsExtended);
        Assert.IsTrue(restored.Frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.IsTrue(restored.Frame.Flags.HasFlag(CanFrameFlags.BRS));
        Assert.AreEqual(payload.Length, restored.Frame.Length);
        CollectionAssert.AreEqual(payload, CopyPayload(restored.Frame));
    }

    [TestMethod(DisplayName = "Merged TX echo flag maps to DriverEcho loopback event")]
    public void MergedTxEchoFlag_MapsToDriverEcho()
    {
        var nativeFrame = new NativeCanFdFrame
        {
            CanId = 0x321,
            Flags = (byte)ZlgCanFdFrameFlags.BitRateSwitch,
        };
        nativeFrame.SetData([0xAA, 0xBB, 0xCC]);

        var data = new NativeCanFdData
        {
            Flag = 1u | (1u << 9),
            Frame = nativeFrame,
        };
        var obj = new NativeDataObject
        {
            DataType = (byte)ZlgDataObjectType.CanOrCanFd,
            Channel = 1,
        };
        obj.WriteCanFdData(data);

        var restored = ZlgFrameConverter.FromNativeDataObject(obj, sequence: 7);

        Assert.AreEqual(CanFrameObservationKind.DriverEcho, restored.ObservationKind);
        Assert.IsTrue(restored.EventFlags.HasFlag(CanFrameEventFlags.Loopback));
        Assert.AreEqual(1, restored.ChannelIndex);
    }

    [TestMethod(DisplayName = "Merged error object maps to error frame and status")]
    public void ErrorDataObject_MapsToErrorFrameAndStatus()
    {
        var nativeError = new NativeErrorData
        {
            Timestamp = 100,
            ErrorType = (byte)ZlgErrorType.BusError,
            ErrorSubType = (byte)ZlgBusErrorSubType.AckError,
            NodeState = (byte)ZlgNodeState.Warning,
            RxErrorCount = 1,
            TxErrorCount = 2,
            ErrorData = 3,
        };
        var obj = new NativeDataObject
        {
            DataType = (byte)ZlgDataObjectType.Error,
            Channel = 1,
        };
        WriteErrorData(ref obj, nativeError);

        var frameEvent = ZlgFrameConverter.FromNativeDataObject(obj, sequence: 10);
        var status = ZlgFrameConverter.ToStatusEvent(obj, sequence: 11);

        Assert.AreEqual(CanFrameKind.Error, frameEvent.Frame.Kind);
        Assert.AreEqual(1, frameEvent.ChannelIndex);
        Assert.IsTrue(frameEvent.EventFlags.HasFlag(CanFrameEventFlags.ErrorResponse));
        Assert.AreEqual((byte)ZlgDataObjectType.Error, frameEvent.NativeStatusCode);
        Assert.AreEqual(CanStatusKind.Bus, status.Kind);
        Assert.AreEqual(CanStatusCode.NativeDriverError, status.Code);
        Assert.AreEqual(CanStatusSeverity.Warning, status.Severity);
        Assert.AreEqual(1, status.ChannelIndex);
    }

    private static byte[] CopyPayload(CanFrame frame)
    {
        var payload = new byte[frame.Length];
        frame.CopyPayloadTo(payload);
        return payload;
    }

    private static unsafe void WriteErrorData(ref NativeDataObject dataObject, NativeErrorData errorData)
    {
        fixed (byte* raw = dataObject.Raw)
        {
            Unsafe.WriteUnaligned(raw, errorData);
        }
    }
}
