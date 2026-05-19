using CanHub.Trace.VectorAsc;

namespace CanHub.Trace.VectorAsc.Tests;

[TestClass]
public class VectorAscReaderClassicTests
{
    [TestMethod(DisplayName = "读取经典CAN数据远程扩展和错误帧")]
    public void ReadText_ClassicRows_ParsesFrames()
    {
        const string asc = """
            date Tue May 19 13:00:00.000 2026
            base hex  timestamps absolute
            Begin Triggerblock Tue May 19 13:00:00.000 2026
             0.001000 1 123 Rx d 3 01 02 03
             0.002000 2 18FF50E5x Tx d 8 00 11 22 33 44 55 66 77
             0.003000 1 200 Rx r 6
             0.004000 1 ErrorFrame
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(4, file.Frames.Count);

        var data = file.Frames[0];
        Assert.AreEqual(TimeSpan.FromMilliseconds(1), data.Timestamp);
        Assert.AreEqual(0, data.ChannelIndex);
        Assert.AreEqual(CanFrameDirection.Receive, data.Direction);
        Assert.AreEqual(CanFrameObservationKind.Bus, data.ObservationKind);
        Assert.AreEqual(CanFrameKind.Data, data.Frame.Kind);
        Assert.AreEqual(CanId.Standard(0x123), data.Frame.Id);
        Assert.AreEqual(3, data.Frame.Length);
        Assert.AreEqual(0x01, data.Frame.GetPayloadByte(0));
        Assert.AreEqual(0x03, data.Frame.GetPayloadByte(2));

        var extended = file.Frames[1];
        Assert.AreEqual(1, extended.ChannelIndex);
        Assert.AreEqual(CanFrameDirection.Transmit, extended.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxConfirmed, extended.ObservationKind);
        Assert.AreEqual(CanId.Extended(0x18FF50E5), extended.Frame.Id);
        Assert.AreEqual(8, extended.Frame.Length);
        Assert.AreEqual(0x77, extended.Frame.GetPayloadByte(7));

        var remote = file.Frames[2];
        Assert.AreEqual(CanFrameKind.Remote, remote.Frame.Kind);
        Assert.AreEqual(CanId.Standard(0x200), remote.Frame.Id);
        Assert.AreEqual(6, remote.Frame.Dlc);
        Assert.AreEqual(0, remote.Frame.Length);

        var error = file.Frames[3];
        Assert.AreEqual(CanFrameKind.Error, error.Frame.Kind);
        Assert.AreEqual(0, error.ChannelIndex);
        Assert.AreEqual(CanFrameDirection.Receive, error.Direction);
        Assert.AreEqual(CanFrameEventFlags.ErrorResponse, error.EventFlags);
        Assert.AreEqual(0, file.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "经典CAN行遵循十进制base")]
    public void ReadText_ClassicDecimalBase_ParsesIdDlcAndDataAsDecimal()
    {
        const string asc = """
            date Tue May 19 13:00:00.000 2026
            base dec  timestamps absolute
            Begin Triggerblock Tue May 19 13:00:00.000 2026
             0.001000 1 291 Rx d 3 1 2 3
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(1, file.Frames.Count);
        var frame = file.Frames[0].Frame;
        Assert.AreEqual(CanId.Standard(0x123), frame.Id);
        Assert.AreEqual(3, frame.Dlc);
        Assert.AreEqual(0x01, frame.GetPayloadByte(0));
        Assert.AreEqual(0x03, frame.GetPayloadByte(2));
    }

    [TestMethod(DisplayName = "严格模式对经典CAN错误DLC抛异常")]
    public void ReadText_StrictMalformedClassicDlc_Throws()
    {
        const string asc = """
            base hex  timestamps absolute
            Begin Triggerblock
             0.001000 1 123 Rx d 9 01 02 03
            End TriggerBlock
            """;

        TestAssert.Throws<FormatException>(
            () => VectorAscReader.ReadText(asc, new VectorAscReadOptions { Strict = true }));
    }

    [TestMethod(DisplayName = "宽松模式对不支持行记录诊断")]
    public void ReadText_UnsupportedRows_AddDiagnostics()
    {
        const string asc = """
            base hex  timestamps absolute
            Begin Triggerblock
             0.001000 LIN 1 Rx 10 01
            unsupported metadata row
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(0, file.Frames.Count);
        Assert.AreEqual(2, file.Diagnostics.Count);
        Assert.IsTrue(file.Diagnostics.All(d => d.Code == VectorAscDiagnosticCodes.UnsupportedLine));
    }
}
