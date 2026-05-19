using CanHub.Trace.VectorAsc;

namespace CanHub.Trace.VectorAsc.Tests;

[TestClass]
public class VectorAscReaderCanFdTests
{
    [TestMethod(DisplayName = "读取CANFD无符号名64字节帧")]
    public void ReadText_CanFdWithoutSymbolicName_ParsesFrame()
    {
        var payload = string.Join(' ', Enumerable.Range(0, 64).Select(i => i.ToString("X2")));
        var asc = $"""
            date Tue May 19 13:00:00.000 2026
            base hex  timestamps absolute
            Begin Triggerblock Tue May 19 13:00:00.000 2026
             0.005000 CANFD   1 Rx        3FF                                   1 0 F 64 {payload}        0    0     3000        0        0        0        0        0
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(1, file.Frames.Count);
        var record = file.Frames[0];
        Assert.IsTrue(record.IsCanFdLine);
        Assert.AreEqual(TimeSpan.FromMilliseconds(5), record.Timestamp);
        Assert.AreEqual(0, record.ChannelIndex);
        Assert.AreEqual(CanFrameDirection.Receive, record.Direction);
        Assert.IsNull(record.SymbolicName);
        Assert.AreEqual(0x3000u, record.CanFdFlags);
        Assert.AreEqual(CanFrameKind.Data, record.Frame.Kind);
        Assert.AreEqual(CanId.Standard(0x3FF), record.Frame.Id);
        Assert.AreEqual(15, record.Frame.Dlc);
        Assert.AreEqual(64, record.Frame.Length);
        Assert.IsTrue(record.Frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.IsTrue(record.Frame.Flags.HasFlag(CanFrameFlags.BRS));
        Assert.IsFalse(record.Frame.Flags.HasFlag(CanFrameFlags.ESI));
        Assert.AreEqual(0x00, record.Frame.GetPayloadByte(0));
        Assert.AreEqual(0x3F, record.Frame.GetPayloadByte(63));
        Assert.AreEqual(0, file.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "读取CANFD带符号名ESI帧")]
    public void ReadText_CanFdWithSymbolicName_ParsesFrame()
    {
        const string asc = """
            date Tue May 19 13:00:00.000 2026
            base hex  timestamps absolute
            Begin Triggerblock Tue May 19 13:00:00.000 2026
             0.006000 CANFD   2 Tx        456                 VehicleStatusFrame 0 1 8  8 AA BB CC DD EE FF 00 11        0    0     5000        0        0        0        0        0
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(1, file.Frames.Count);
        var record = file.Frames[0];
        Assert.AreEqual(1, record.ChannelIndex);
        Assert.AreEqual(CanFrameDirection.Transmit, record.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxConfirmed, record.ObservationKind);
        Assert.AreEqual("VehicleStatusFrame", record.SymbolicName);
        Assert.AreEqual(0x5000u, record.CanFdFlags);
        Assert.AreEqual(CanId.Standard(0x456), record.Frame.Id);
        Assert.AreEqual(8, record.Frame.Dlc);
        Assert.AreEqual(8, record.Frame.Length);
        Assert.IsTrue(record.Frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.IsFalse(record.Frame.Flags.HasFlag(CanFrameFlags.BRS));
        Assert.IsTrue(record.Frame.Flags.HasFlag(CanFrameFlags.ESI));
        Assert.AreEqual(0xAA, record.Frame.GetPayloadByte(0));
        Assert.AreEqual(0x11, record.Frame.GetPayloadByte(7));
        Assert.AreEqual(0, file.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "CANFD flags与BRS ESI不一致时产生诊断")]
    public void ReadText_CanFdFlagsMismatch_AddsDiagnostic()
    {
        const string asc = """
            date Tue May 19 13:00:00.000 2026
            base hex  timestamps absolute
            Begin Triggerblock Tue May 19 13:00:00.000 2026
             0.006000 CANFD   1 Rx        456                                   0 0 8  8 AA BB CC DD EE FF 00 11        0    0     7000        0        0        0        0        0
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(1, file.Frames.Count);
        Assert.AreEqual(1, file.Diagnostics.Count);
        Assert.AreEqual(VectorAscDiagnosticCodes.CanFdFlagsMismatch, file.Diagnostics[0].Code);
    }

    [TestMethod(DisplayName = "读取CANoe风格CANFD flags不产生误报")]
    public void ReadText_CanoeStyleCanFdFlags_DoNotAddDiagnostic()
    {
        const string asc = """
            date Tue May 19 13:00:00.000 2026
            base hex  timestamps absolute
            Begin Triggerblock Tue May 19 13:00:00.000 2026
             0.006000 CANFD   1 Rx        456                                   0 0 8  8 AA BB CC DD EE FF 00 11        0    0   200000        0        0        0        0        0
             0.007000 CANFD   1 Tx        457                                   1 0 8  8 01 02 03 04 05 06 07 08        0    0   303040        0        0        0        0        0
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(2, file.Frames.Count);
        Assert.AreEqual(0, file.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "读取CANoe导出的CANFD边界flags不产生误报")]
    public void ReadText_CanoeExportedCanFdEdgeFlags_DoNotAddDiagnostic()
    {
        const string asc = """
            date Tue May 19 04:16:26.206 pm 2026
            base hex  timestamps absolute
            internal events logged
             0.001000 CANFD   1 Rx        101                                   0 0 4  4 01 11 00 00   164031   86   200000     4fbf 46500250 4b280150 20011736 2000091c
             0.002000 CANFD   1 Rx        103                                   0 0 4  0    88031   48   200010     7535 46500250 4b280150 20011736 2000091c
             0.003000 CANFD   1 Rx        105                                   0 0 4  4 01 11 00 00   190031   99   301000 f8010760 46500250 4b280150 20011736 2000091c
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(3, file.Frames.Count);
        Assert.AreEqual(0, file.Diagnostics.Count);
        Assert.AreEqual(0, file.Frames[1].Frame.Length);
    }

    [TestMethod(DisplayName = "严格模式对CANFD DLC和长度不一致抛异常")]
    public void ReadText_StrictCanFdDlcLengthMismatch_Throws()
    {
        const string asc = """
            base hex  timestamps absolute
            Begin Triggerblock
             0.006000 CANFD   1 Rx        456                                   0 0 F  8 AA BB CC DD EE FF 00 11        0    0     1000        0        0        0        0        0
            End TriggerBlock
            """;

        TestAssert.Throws<FormatException>(
            () => VectorAscReader.ReadText(asc, new VectorAscReadOptions { Strict = true }));
    }
}
