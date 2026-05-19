using CanHub.Trace.VectorAsc;

namespace CanHub.Trace.VectorAsc.Tests;

[TestClass]
public class VectorAscReaderHeaderTests
{
    [TestMethod(DisplayName = "读取ASC头部元数据")]
    public void ReadText_HeaderMetadata_Parses()
    {
        const string asc = """
            date Tue May 19 13:00:00.123 2026
            base hex  timestamps absolute
            internal events logged
            Begin Triggerblock Tue May 19 13:00:01.456 2026
             0.000000 Start of measurement
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(VectorAscNumericBase.Hex, file.NumericBase);
        Assert.AreEqual(VectorAscTimestampFormat.Absolute, file.TimestampFormat);
        Assert.IsTrue(file.InternalEventsLogged);
        Assert.AreEqual("Tue May 19 13:00:00.123 2026", file.RawDateText);
        Assert.AreEqual("Tue May 19 13:00:01.456 2026", file.RawTriggerBlockText);
        Assert.IsNotNull(file.FileDate);
        Assert.IsNotNull(file.TriggerBlockStart);
        Assert.AreEqual(0, file.Frames.Count);
        Assert.AreEqual(0, file.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "读取十进制和相对时间头部")]
    public void ReadText_DecimalRelativeHeader_Parses()
    {
        const string asc = """
            date Tue May 19 13:00:00 2026
            base dec  timestamps relative
            Begin TriggerBlock Tue May 19 13:00:00 2026
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.AreEqual(VectorAscNumericBase.Decimal, file.NumericBase);
        Assert.AreEqual(VectorAscTimestampFormat.Relative, file.TimestampFormat);
        Assert.IsFalse(file.InternalEventsLogged);
        Assert.AreEqual(0, file.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "读取未启用内部事件头部")]
    public void ReadText_NoInternalEventsLogged_Parses()
    {
        const string asc = """
            date Sat Nov 30 10:08:51.099 am 2024
            base hex  timestamps absolute
            no internal events logged
            Begin Triggerblock Fre Okt 12 11:21:07 2007
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.IsFalse(file.InternalEventsLogged);
        Assert.IsNotNull(file.FileDate);
        Assert.IsNotNull(file.TriggerBlockStart);
        Assert.AreEqual(0, file.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "读取本地化ASC日期头部")]
    public void ReadText_LocalizedDateHeaders_Parse()
    {
        const string asc = """
            date 周四 1月 29 01:49:46.603 下午 2026
            base hex  timestamps absolute
            Begin Triggerblock Fri May 1 12:0:41 am 2026
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.IsNotNull(file.FileDate);
        Assert.IsNotNull(file.TriggerBlockStart);
        Assert.AreEqual(0, file.Diagnostics.Count);
    }

    [TestMethod(DisplayName = "宽松模式记录无法识别日期诊断")]
    public void ReadText_UnparseableDate_AddsDiagnostic()
    {
        const string asc = """
            date Martian Calendar
            base hex  timestamps absolute
            Begin Triggerblock Martian Calendar
            End TriggerBlock
            """;

        var file = VectorAscReader.ReadText(asc);

        Assert.IsNull(file.FileDate);
        Assert.IsNull(file.TriggerBlockStart);
        Assert.AreEqual(2, file.Diagnostics.Count);
        Assert.IsTrue(file.Diagnostics.All(d => d.Code == VectorAscDiagnosticCodes.UnparseableDate));
    }

    [TestMethod(DisplayName = "严格模式对无法识别日期抛异常")]
    public void ReadText_StrictUnparseableDate_Throws()
    {
        const string asc = """
            date Martian Calendar
            base hex  timestamps absolute
            End TriggerBlock
            """;

        TestAssert.Throws<FormatException>(
            () => VectorAscReader.ReadText(asc, new VectorAscReadOptions { Strict = true }));
    }

    [TestMethod(DisplayName = "从文件读取ASC内容")]
    public void ReadFile_ExistingAscFile_Parses()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.asc");
        try
        {
            File.WriteAllText(path, """
                base hex  timestamps absolute
                Begin Triggerblock
                 0.001000 1 123 Rx d 1 AA
                End TriggerBlock
                """);

            var file = VectorAscReader.ReadFile(path);

            Assert.AreEqual(1, file.Frames.Count);
            Assert.AreEqual(0xAA, file.Frames[0].Frame.GetPayloadByte(0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod(DisplayName = "流式读取文件帧并通过回调收集诊断")]
    public void ReadFileFrames_StreamsFramesAndReportsDiagnostics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.asc");
        var diagnostics = new List<VectorAscDiagnostic>();
        try
        {
            File.WriteAllText(path, """
                base hex  timestamps absolute
                Begin Triggerblock
                 0.001000 1 123 Rx d 1 AA
                unsupported metadata row
                 0.002000 1 124 Rx d 1 BB
                End TriggerBlock
                """);

            var frames = VectorAscReader.ReadFileFrames(
                path,
                new VectorAscReadOptions { DiagnosticSink = diagnostics.Add }).ToArray();

            Assert.AreEqual(2, frames.Length);
            Assert.AreEqual(0xAA, frames[0].Frame.GetPayloadByte(0));
            Assert.AreEqual(0xBB, frames[1].Frame.GetPayloadByte(0));
            Assert.AreEqual(1, diagnostics.Count);
            Assert.AreEqual(VectorAscDiagnosticCodes.UnsupportedLine, diagnostics[0].Code);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
