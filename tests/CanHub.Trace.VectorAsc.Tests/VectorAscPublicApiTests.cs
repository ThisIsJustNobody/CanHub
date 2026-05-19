using CanHub.Trace.VectorAsc;

namespace CanHub.Trace.VectorAsc.Tests;

[TestClass]
public class VectorAscPublicApiTests
{
    [TestMethod(DisplayName = "读取API对空参数抛出明确异常")]
    public void ReaderApis_InvalidArguments_ThrowClearExceptions()
    {
        TestAssert.Throws<ArgumentNullException>(() => VectorAscReader.ReadText(null!));
        TestAssert.Throws<ArgumentException>(() => VectorAscReader.ReadFile(" "));
        TestAssert.Throws<ArgumentException>(() => VectorAscReader.ReadFileFrames(" ").ToArray());
        TestAssert.Throws<ArgumentNullException>(() => VectorAscReader.Read(null!));
        TestAssert.Throws<ArgumentNullException>(() => VectorAscReader.ReadFrames(null!).ToArray());
    }

    [TestMethod(DisplayName = "写入API对空参数抛出明确异常")]
    public void WriterApis_InvalidArguments_ThrowClearExceptions()
    {
        TestAssert.Throws<ArgumentNullException>(() => VectorAscWriter.WriteText(null!));
        TestAssert.Throws<ArgumentException>(() => VectorAscWriter.WriteFile(" ", []));
        TestAssert.Throws<ArgumentNullException>(() => VectorAscWriter.Write(null!, []));
        TestAssert.Throws<ArgumentNullException>(() => VectorAscWriter.Write(TextWriter.Null, null!));
    }

    [TestMethod(DisplayName = "写入文件时帧集合为空不会创建目标文件")]
    public void WriteFile_NullFrames_DoesNotCreateTargetFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.asc");
        try
        {
            TestAssert.Throws<ArgumentNullException>(() => VectorAscWriter.WriteFile(path, null!));

            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod(DisplayName = "严格模式对不支持行抛异常")]
    public void ReadText_StrictUnsupportedLine_Throws()
    {
        const string asc = """
            base hex  timestamps absolute
            unsupported metadata row
            """;

        TestAssert.Throws<FormatException>(
            () => VectorAscReader.ReadText(asc, new VectorAscReadOptions { Strict = true }));
    }

    [TestMethod(DisplayName = "转换API对空ASC记录抛异常")]
    public void ToFrameEvent_NullRecord_Throws()
    {
        TestAssert.Throws<ArgumentNullException>(() => VectorAscCanHubConversion.ToFrameEvent(null!));
    }
}
