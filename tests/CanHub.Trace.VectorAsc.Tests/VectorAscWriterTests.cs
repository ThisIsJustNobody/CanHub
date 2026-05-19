using CanHub.Trace.VectorAsc;

namespace CanHub.Trace.VectorAsc.Tests;

[TestClass]
public class VectorAscWriterTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 5, 19, 13, 0, 0, 123, TimeSpan.Zero);

    [TestMethod(DisplayName = "写入ASC头部和经典CAN帧")]
    public void WriteText_ClassicFrames_WritesHeaderAndRows()
    {
        var records = new[]
        {
            new VectorAscFrame
            {
                Timestamp = TimeSpan.FromMilliseconds(1),
                ChannelIndex = 0,
                Direction = CanFrameDirection.Receive,
                ObservationKind = CanFrameObservationKind.Bus,
                Frame = CanFrame.CreateData(CanId.Standard(0x123), [0x01, 0x02, 0x03])
            },
            new VectorAscFrame
            {
                Timestamp = TimeSpan.FromMilliseconds(2),
                ChannelIndex = 0,
                Direction = CanFrameDirection.Receive,
                ObservationKind = CanFrameObservationKind.Bus,
                Frame = CanFrame.CreateRemote(CanId.Standard(0x200), 6)
            },
            new VectorAscFrame
            {
                Timestamp = TimeSpan.FromMilliseconds(3),
                ChannelIndex = 0,
                Direction = CanFrameDirection.Receive,
                ObservationKind = CanFrameObservationKind.Bus,
                EventFlags = CanFrameEventFlags.ErrorResponse,
                Frame = CanFrame.CreateError()
            }
        };

        var text = VectorAscWriter.WriteText(records, new VectorAscWriteOptions { StartTime = StartTime });

        StringAssert.Contains(text, "date Tue May 19 13:00:00.123 2026");
        StringAssert.Contains(text, "base hex  timestamps absolute");
        StringAssert.Contains(text, "internal events logged");
        StringAssert.Contains(text, "Begin Triggerblock Tue May 19 13:00:00.123 2026");
        StringAssert.Contains(text, "0.000000 Start of measurement");
        StringAssert.Contains(text, "0.001000 1  123");
        StringAssert.Contains(text, "Rx   d 3 01 02 03");
        StringAssert.Contains(text, "0.002000 1  200");
        StringAssert.Contains(text, "Rx   r 6");
        StringAssert.Contains(text, "0.003000 1  ErrorFrame");
        StringAssert.Contains(text, "End TriggerBlock");

        var reparsed = VectorAscReader.ReadText(text);
        Assert.AreEqual(3, reparsed.Frames.Count);
        Assert.AreEqual(CanFrameKind.Data, reparsed.Frames[0].Frame.Kind);
        Assert.AreEqual(CanFrameKind.Remote, reparsed.Frames[1].Frame.Kind);
        Assert.AreEqual(CanFrameKind.Error, reparsed.Frames[2].Frame.Kind);
    }

    [TestMethod(DisplayName = "写入CANFD帧并可回读")]
    public void WriteText_CanFdFrame_WritesCanFdRowAndRoundTrips()
    {
        var payload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var records = new[]
        {
            new VectorAscFrame
            {
                Timestamp = TimeSpan.FromMilliseconds(5),
                ChannelIndex = 0,
                Direction = CanFrameDirection.Receive,
                ObservationKind = CanFrameObservationKind.Bus,
                Frame = CanFrame.CreateFdData(CanId.Standard(0x3FF), payload, bitRateSwitch: true),
                IsCanFdLine = true
            }
        };

        var text = VectorAscWriter.WriteText(records, new VectorAscWriteOptions { StartTime = StartTime });

        StringAssert.Contains(text, "0.005000 CANFD");
        StringAssert.Contains(text, "3FF");
        StringAssert.Contains(text, "1 0 f 64");
        StringAssert.Contains(text, "00 01 02 03");
        StringAssert.Contains(text, "3000");

        var reparsed = VectorAscReader.ReadText(text);
        Assert.AreEqual(1, reparsed.Frames.Count);
        var frame = reparsed.Frames[0].Frame;
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.BRS));
        Assert.AreEqual(64, frame.Length);
        Assert.AreEqual(0x3F, frame.GetPayloadByte(63));
        Assert.AreEqual(0, reparsed.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "写入CANFD使用CANoe导出风格的保守格式")]
    public void WriteText_CanFdFrame_WritesConservativeCanoeStyleShape()
    {
        var records = new[]
        {
            new VectorAscFrame
            {
                Timestamp = TimeSpan.FromMilliseconds(1),
                ChannelIndex = 0,
                Direction = CanFrameDirection.Receive,
                ObservationKind = CanFrameObservationKind.Bus,
                Frame = CanFrame.CreateFdData(CanId.Standard(0x100), [0x01], bitRateSwitch: false),
                IsCanFdLine = true
            },
            new VectorAscFrame
            {
                Timestamp = TimeSpan.FromMilliseconds(2),
                ChannelIndex = 0,
                Direction = CanFrameDirection.Transmit,
                ObservationKind = CanFrameObservationKind.TxConfirmed,
                Frame = CanFrame.CreateFdData(CanId.Standard(0x101), [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08], bitRateSwitch: true),
                IsCanFdLine = true,
                SymbolicName = "ProbeFdName"
            }
        };

        var text = VectorAscWriter.WriteText(records, new VectorAscWriteOptions { StartTime = StartTime });

        StringAssert.Contains(text, "0 0 1  1 01        0    0   200000        0        0        0        0        0");
        StringAssert.Contains(text, "1 0 8  8 01 02 03 04 05 06 07 08        0    0   303040        0        0        0        0        0");
        Assert.IsFalse(text.Contains("ProbeFdName", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("     1000", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("     3000", StringComparison.Ordinal));
    }

    [TestMethod(DisplayName = "写入ASC文件")]
    public void WriteFile_TargetPath_WritesContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.asc");
        try
        {
            var records = new[]
            {
                new VectorAscFrame
                {
                    Timestamp = TimeSpan.FromMilliseconds(1),
                    ChannelIndex = 0,
                    Direction = CanFrameDirection.Receive,
                    ObservationKind = CanFrameObservationKind.Bus,
                    Frame = CanFrame.CreateData(CanId.Standard(0x123), [0xAA])
                }
            };

            VectorAscWriter.WriteFile(path, records, new VectorAscWriteOptions { StartTime = StartTime });

            var text = File.ReadAllText(path);
            StringAssert.Contains(text, "0.001000 1  123");
            var reparsed = VectorAscReader.ReadFile(path);
            Assert.AreEqual(1, reparsed.Frames.Count);
            Assert.AreEqual(0xAA, reparsed.Frames[0].Frame.GetPayloadByte(0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod(DisplayName = "相对时间写出使用相邻帧delta")]
    public void WriteText_RelativeTimestamps_WritesDeltas()
    {
        var records = new[]
        {
            new VectorAscFrame
            {
                Timestamp = TimeSpan.FromMilliseconds(1),
                ChannelIndex = 0,
                Direction = CanFrameDirection.Receive,
                ObservationKind = CanFrameObservationKind.Bus,
                Frame = CanFrame.CreateData(CanId.Standard(0x100), [0x01])
            },
            new VectorAscFrame
            {
                Timestamp = TimeSpan.FromMilliseconds(3),
                ChannelIndex = 0,
                Direction = CanFrameDirection.Receive,
                ObservationKind = CanFrameObservationKind.Bus,
                Frame = CanFrame.CreateData(CanId.Standard(0x101), [0x02])
            }
        };

        var text = VectorAscWriter.WriteText(
            records,
            new VectorAscWriteOptions
            {
                StartTime = StartTime,
                TimestampFormat = VectorAscTimestampFormat.Relative
            });

        StringAssert.Contains(text, "base hex  timestamps relative");
        StringAssert.Contains(text, "0.001000 1  100");
        StringAssert.Contains(text, "0.002000 1  101");

        var reparsed = VectorAscReader.ReadText(text);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1), reparsed.Frames[0].Timestamp);
        Assert.AreEqual(TimeSpan.FromMilliseconds(3), reparsed.Frames[1].Timestamp);
    }
}
