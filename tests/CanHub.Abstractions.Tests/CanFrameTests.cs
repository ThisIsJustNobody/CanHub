using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanFrameTests
{
    private static readonly CanId StdId = CanId.Standard(0x100);
    private static readonly CanId ExtId = CanId.Extended(0x100000);

    #region Default Values

    [TestMethod(DisplayName = "默认Kind为None")]
    public void Default_Kind_IsNone()
    {
        var frame = default(CanFrame);
        Assert.AreEqual(CanFrameKind.None, frame.Kind);
    }

    [TestMethod(DisplayName = "默认帧未初始化")]
    public void Default_IsNotInitialized()
    {
        var frame = default(CanFrame);
        Assert.IsFalse(frame.IsInitialized);
    }

    [TestMethod(DisplayName = "默认帧不等于空数据帧")]
    public void Default_IsNotEquivalentToEmptyDataFrameWithIdZero()
    {
        var frame = default(CanFrame);
        var data = CanFrame.CreateData(CanId.Standard(0), ReadOnlySpan<byte>.Empty);
        Assert.AreNotEqual(data, frame);
        Assert.AreNotEqual(CanFrameKind.Data, frame.Kind);
    }

    #endregion

    #region Classic Data

    [TestMethod(DisplayName = "空载荷经典帧创建成功")]
    public void CreateData_Payload0_Succeeds()
    {
        var frame = CanFrame.CreateData(StdId, ReadOnlySpan<byte>.Empty);
        Assert.AreEqual(CanFrameKind.Data, frame.Kind);
        Assert.IsTrue(frame.IsInitialized);
        Assert.AreEqual(0, frame.Length);
        Assert.IsTrue(frame.IsEmpty);
        Assert.AreEqual(0, frame.Dlc);
    }

    [TestMethod(DisplayName = "经典帧1-8字节载荷创建成功")]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(8)]
    public void CreateData_Payload1To8_Succeeds(int length)
    {
        var payload = new byte[length];
        payload[0] = 0xAB;
        var frame = CanFrame.CreateData(StdId, payload);
        Assert.AreEqual(length, frame.Length);
        Assert.AreEqual(payload[0], frame.GetPayloadByte(0));
        Assert.IsFalse(frame.IsEmpty);
    }

    [TestMethod(DisplayName = "超出经典帧载荷上限抛异常")]
    [DataRow(9)]
    [DataRow(32)]
    [DataRow(64)]
    public void CreateData_PayloadExceedsClassic_Throws(int length)
    {
        var payload = new byte[length];
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.CreateData(StdId, payload));
    }

    [TestMethod(DisplayName = "经典帧设置FD标志抛异常")]
    public void CreateData_FdFlagSet_Throws()
    {
        TestAssert.Throws<ArgumentException>(
            () => CanFrame.CreateData(StdId, [1], CanFrameFlags.FD));
    }

    [TestMethod(DisplayName = "经典帧设置BRS标志抛异常")]
    public void CreateData_BrsFlagSet_Throws()
    {
        TestAssert.Throws<ArgumentException>(
            () => CanFrame.CreateData(StdId, [1], CanFrameFlags.BRS));
    }

    [TestMethod(DisplayName = "经典帧设置ESI标志抛异常")]
    public void CreateData_EsiFlagSet_Throws()
    {
        TestAssert.Throws<ArgumentException>(
            () => CanFrame.CreateData(StdId, [1], CanFrameFlags.ESI));
    }

    #endregion

    #region FD Data

    [TestMethod(DisplayName = "空载荷FD帧创建成功")]
    public void CreateFdData_Payload0_Succeeds()
    {
        var frame = CanFrame.CreateFdData(StdId, ReadOnlySpan<byte>.Empty);
        Assert.AreEqual(CanFrameKind.Data, frame.Kind);
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.AreEqual(0, frame.Length);
        Assert.AreEqual(0, frame.Dlc);
    }

    [TestMethod(DisplayName = "FD帧有效载荷长度创建成功")]
    [DataRow(1)]
    [DataRow(8)]
    [DataRow(12)]
    [DataRow(16)]
    [DataRow(20)]
    [DataRow(24)]
    [DataRow(32)]
    [DataRow(48)]
    [DataRow(64)]
    public void CreateFdData_ValidLengths_Succeeds(int length)
    {
        var payload = new byte[length];
        payload[0] = 0xCD;
        var frame = CanFrame.CreateFdData(StdId, payload);
        Assert.AreEqual(length, frame.Length);
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.AreEqual(payload[0], frame.GetPayloadByte(0));
    }

    [TestMethod(DisplayName = "超出FD帧最大长度抛异常")]
    [DataRow(65)]
    [DataRow(100)]
    [DataRow(128)]
    public void CreateFdData_ExceedsMax_Throws(int length)
    {
        var payload = new byte[length];
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.CreateFdData(StdId, payload));
    }

    [TestMethod(DisplayName = "非DLC整数倍长度抛异常")]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(13)]
    [DataRow(15)]
    [DataRow(30)]
    [DataRow(50)]
    public void CreateFdData_NonDlcRepresentableLength_Throws(int length)
    {
        var payload = new byte[length];
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.CreateFdData(StdId, payload));
    }

    [TestMethod(DisplayName = "FD帧自动设置FD标志")]
    public void CreateFdData_AutoSetsFdFlag()
    {
        var frame = CanFrame.CreateFdData(StdId, [1]);
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.FD));
    }

    [TestMethod(DisplayName = "FD帧开启BRS创建成功")]
    public void CreateFdData_BrsWithFd_Succeeds()
    {
        var frame = CanFrame.CreateFdData(StdId, [1], bitRateSwitch: true);
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.BRS));
    }

    [TestMethod(DisplayName = "FD帧开启ESI创建成功")]
    public void CreateFdData_EsiWithFd_Succeeds()
    {
        var frame = CanFrame.CreateFdData(StdId, [1], errorStateIndicator: true);
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.FD));
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.ESI));
    }

    #endregion

    #region Remote

    [TestMethod(DisplayName = "远程帧DLC为0创建成功")]
    public void CreateRemote_Dlc0_Succeeds()
    {
        var frame = CanFrame.CreateRemote(StdId, 0);
        Assert.AreEqual(CanFrameKind.Remote, frame.Kind);
        Assert.AreEqual(0, frame.Dlc);
        Assert.AreEqual(0, frame.Length);
        Assert.IsTrue(frame.IsEmpty);
    }

    [TestMethod(DisplayName = "远程帧DLC为8创建成功")]
    public void CreateRemote_Dlc8_Succeeds()
    {
        var frame = CanFrame.CreateRemote(StdId, 8);
        Assert.AreEqual(8, frame.Dlc);
    }

    [TestMethod(DisplayName = "远程帧DLC超8抛异常")]
    [DataRow(9)]
    [DataRow(15)]
    public void CreateRemote_DlcExceeds8_Throws(int dlc)
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.CreateRemote(StdId, (byte)dlc));
    }

    #endregion

    #region Error

    [TestMethod(DisplayName = "错误帧默认错误码创建成功")]
    public void CreateError_DefaultErrorCode_Succeeds()
    {
        var frame = CanFrame.CreateError();
        Assert.AreEqual(CanFrameKind.Error, frame.Kind);
        Assert.AreEqual(0u, frame.ErrorCode);
        Assert.AreEqual(0, frame.Length);
        Assert.AreEqual(default(CanId), frame.Id);
    }

    [TestMethod(DisplayName = "带错误码的错误帧创建成功")]
    public void CreateError_WithErrorCode_Succeeds()
    {
        var frame = CanFrame.CreateError(0xDEADBEEF);
        Assert.AreEqual(0xDEADBEEFu, frame.ErrorCode);
        Assert.AreEqual(CanFrameKind.Error, frame.Kind);
        Assert.AreEqual(default(CanId), frame.Id);
    }

    [TestMethod(DisplayName = "错误帧载荷复制公开错误字节")]
    public void CreateError_WithPayload_CopiesPublicErrorBytes()
    {
        byte[] payload = [0x01, 0x04, 0x08, 0x10];
        var frame = CanFrame.CreateError(0x200, payload);
        Assert.AreEqual(CanFrameKind.Error, frame.Kind);
        Assert.AreEqual(0x200u, frame.ErrorCode);
        Assert.AreEqual(payload.Length, frame.PayloadLength);
        Assert.AreEqual((byte)payload.Length, frame.Dlc);
        Assert.AreEqual(default(CanId), frame.Id);
        for (int i = 0; i < payload.Length; i++)
            Assert.AreEqual(payload[i], frame.GetPayloadByte(i));
    }

    [TestMethod(DisplayName = "错误帧载荷超8抛异常")]
    public void CreateError_PayloadExceeds8_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.CreateError(0x200, new byte[9]));
    }

    #endregion

    #region Overload

    [TestMethod(DisplayName = "过载帧创建成功")]
    public void CreateOverload_Succeeds()
    {
        var frame = CanFrame.CreateOverload();
        Assert.AreEqual(CanFrameKind.Overload, frame.Kind);
        Assert.AreEqual(0, frame.Length);
        Assert.AreEqual(default(CanId), frame.Id);
    }

    #endregion

    #region DLC Mapping

    [TestMethod(DisplayName = "DLC转长度映射正确")]
    [DataRow((byte)0, 0)]
    [DataRow((byte)8, 8)]
    [DataRow((byte)9, 12)]
    [DataRow((byte)10, 16)]
    [DataRow((byte)11, 20)]
    [DataRow((byte)12, 24)]
    [DataRow((byte)13, 32)]
    [DataRow((byte)14, 48)]
    [DataRow((byte)15, 64)]
    public void DlcToLength_MapsCorrectly(byte dlc, int expected)
    {
        Assert.AreEqual(expected, CanFrame.DlcToLength(dlc));
    }

    [TestMethod(DisplayName = "无效DLC转长度抛异常")]
    [DataRow((byte)16)]
    [DataRow(byte.MaxValue)]
    public void DlcToLength_InvalidDlc_Throws(byte dlc)
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.DlcToLength(dlc));
    }

    [TestMethod(DisplayName = "长度转DLC映射正确")]
    [DataRow(0, (byte)0)]
    [DataRow(8, (byte)8)]
    [DataRow(12, (byte)9)]
    [DataRow(16, (byte)10)]
    [DataRow(20, (byte)11)]
    [DataRow(24, (byte)12)]
    [DataRow(32, (byte)13)]
    [DataRow(48, (byte)14)]
    [DataRow(64, (byte)15)]
    public void LengthToDlc_MapsCorrectly(int length, byte expectedDlc)
    {
        Assert.AreEqual(expectedDlc, CanFrame.LengthToDlc(length));
    }

    [TestMethod(DisplayName = "非标准长度TryLengthToDlc返回false")]
    [DataRow(9)]
    [DataRow(15)]
    [DataRow(30)]
    [DataRow(50)]
    public void TryLengthToDlc_NonStandardLength_ReturnsFalse(int length)
    {
        Assert.IsFalse(CanFrame.TryLengthToDlc(length, out _));
    }

    [TestMethod(DisplayName = "非标准长度转DLC抛异常")]
    [DataRow(9)]
    [DataRow(15)]
    [DataRow(30)]
    [DataRow(50)]
    public void LengthToDlc_NonStandardLength_Throws(int length)
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.LengthToDlc(length));
    }

    [TestMethod(DisplayName = "有效长度TryLengthToDlc返回true")]
    [DataRow(0, (byte)0)]
    [DataRow(8, (byte)8)]
    [DataRow(12, (byte)9)]
    [DataRow(16, (byte)10)]
    [DataRow(20, (byte)11)]
    [DataRow(24, (byte)12)]
    [DataRow(32, (byte)13)]
    [DataRow(48, (byte)14)]
    [DataRow(64, (byte)15)]
    public void TryLengthToDlc_ValidLengths_ReturnsTrue(int length, byte expectedDlc)
    {
        Assert.IsTrue(CanFrame.TryLengthToDlc(length, out byte dlc));
        Assert.AreEqual(expectedDlc, dlc);
    }

    [TestMethod(DisplayName = "负长度转DLC抛异常")]
    public void LengthToDlc_NegativeLength_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.LengthToDlc(-1));
    }

    [TestMethod(DisplayName = "长度超64转DLC抛异常")]
    public void LengthToDlc_LengthExceeds64_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrame.LengthToDlc(65));
    }

    [TestMethod(DisplayName = "有效载荷长度范围验证")]
    [DataRow(0, false, true)]
    [DataRow(8, false, true)]
    [DataRow(9, false, false)]
    [DataRow(0, true, true)]
    [DataRow(9, true, false)]
    [DataRow(12, true, true)]
    [DataRow(64, true, true)]
    [DataRow(65, true, false)]
    public void IsValidPayloadLength_ValidateRanges(int length, bool allowFd, bool expected)
    {
        Assert.AreEqual(expected, CanFrame.IsValidPayloadLength(length, allowFd));
    }

    [TestMethod(DisplayName = "有效DLC范围检查")]
    [DataRow((byte)0, true)]
    [DataRow((byte)8, true)]
    [DataRow((byte)15, true)]
    [DataRow((byte)16, false)]
    public void IsValidDlc_RangeCheck(byte dlc, bool expected)
    {
        Assert.AreEqual(expected, CanFrame.IsValidDlc(dlc));
    }

    #endregion

    #region TryCreate

    [TestMethod(DisplayName = "TryCreateData有效输入返回true")]
    public void TryCreateData_ValidInput_ReturnsTrue()
    {
        bool ok = CanFrame.TryCreateData(StdId, [1, 2], CanFrameFlags.None, out var frame);
        Assert.IsTrue(ok);
        Assert.AreEqual(CanFrameKind.Data, frame.Kind);
    }

    [TestMethod(DisplayName = "TryCreateData无flags重载创建经典CAN帧")]
    public void TryCreateData_WithoutFlags_ValidInput_ReturnsTrue()
    {
        bool ok = CanFrame.TryCreateData(StdId, [1, 2], out var frame);
        Assert.IsTrue(ok);
        Assert.AreEqual(CanFrameKind.Data, frame.Kind);
        Assert.AreEqual(CanFrameFlags.None, frame.Flags);
    }

    [TestMethod(DisplayName = "TryCreateData载荷过长返回false")]
    public void TryCreateData_TooLong_ReturnsFalse()
    {
        bool ok = CanFrame.TryCreateData(StdId, new byte[9], CanFrameFlags.None, out var frame);
        Assert.IsFalse(ok);
        Assert.AreEqual(default, frame);
        Assert.AreEqual(CanFrameKind.None, frame.Kind);
        Assert.IsFalse(frame.IsInitialized);
    }

    [TestMethod(DisplayName = "TryCreateData含FD标志返回false")]
    public void TryCreateData_FdFlag_ReturnsFalse()
    {
        bool ok = CanFrame.TryCreateData(StdId, [1], CanFrameFlags.FD, out var frame);
        Assert.IsFalse(ok);
    }

    [TestMethod(DisplayName = "TryCreateFdData有效输入返回true")]
    public void TryCreateFdData_ValidInput_ReturnsTrue()
    {
        bool ok = CanFrame.TryCreateFdData(StdId, new byte[64], out var frame);
        Assert.IsTrue(ok);
        Assert.IsTrue(frame.Flags.HasFlag(CanFrameFlags.FD));
    }

    [TestMethod(DisplayName = "TryCreateFdData载荷过长返回false")]
    public void TryCreateFdData_TooLong_ReturnsFalse()
    {
        bool ok = CanFrame.TryCreateFdData(StdId, new byte[65], out var frame);
        Assert.IsFalse(ok);
        Assert.AreEqual(default, frame);
        Assert.AreEqual(CanFrameKind.None, frame.Kind);
        Assert.IsFalse(frame.IsInitialized);
    }

    [TestMethod(DisplayName = "TryCreateRemote有效输入返回true")]
    public void TryCreateRemote_ValidInput_ReturnsTrue()
    {
        bool ok = CanFrame.TryCreateRemote(StdId, 8, out var frame);
        Assert.IsTrue(ok);
        Assert.AreEqual(CanFrameKind.Remote, frame.Kind);
    }

    [TestMethod(DisplayName = "TryCreateRemote DLC超8返回false")]
    [DataRow(9)]
    [DataRow(15)]
    public void TryCreateRemote_DlcExceeds8_ReturnsFalse(int dlc)
    {
        bool ok = CanFrame.TryCreateRemote(StdId, (byte)dlc, out var frame);
        Assert.IsFalse(ok);
        Assert.AreEqual(default, frame);
    }

    [TestMethod(DisplayName = "TryCreateError有效输入返回true")]
    public void TryCreateError_ValidInput_ReturnsTrue()
    {
        bool ok = CanFrame.TryCreateError(42, out var frame);
        Assert.IsTrue(ok);
        Assert.AreEqual(42u, frame.ErrorCode);
        Assert.AreEqual(default(CanId), frame.Id);
    }

    [TestMethod(DisplayName = "TryCreateError带载荷返回true")]
    public void TryCreateError_WithPayload_ReturnsTrue()
    {
        bool ok = CanFrame.TryCreateError(42, [0xAA, 0xBB], out var frame);
        Assert.IsTrue(ok);
        Assert.AreEqual(2, frame.PayloadLength);
        Assert.AreEqual(0xAA, frame.GetPayloadByte(0));
        Assert.AreEqual(0xBB, frame.GetPayloadByte(1));
    }

    [TestMethod(DisplayName = "TryCreateError载荷超8返回false")]
    public void TryCreateError_PayloadExceeds8_ReturnsFalse()
    {
        bool ok = CanFrame.TryCreateError(42, new byte[9], out var frame);
        Assert.IsFalse(ok);
        Assert.AreEqual(default, frame);
    }

    [TestMethod(DisplayName = "TryCreateOverload有效输入返回true")]
    public void TryCreateOverload_ValidInput_ReturnsTrue()
    {
        bool ok = CanFrame.TryCreateOverload(out var frame);
        Assert.IsTrue(ok);
        Assert.AreEqual(CanFrameKind.Overload, frame.Kind);
        Assert.AreEqual(default(CanId), frame.Id);
    }

    #endregion

    #region Equality and HashCode

    [TestMethod(DisplayName = "相同载荷帧相等返回true")]
    public void Equals_SamePayload_ReturnsTrue()
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var a = CanFrame.CreateData(StdId, payload);
        var b = CanFrame.CreateData(StdId, payload);
        Assert.IsTrue(a.Equals(b));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "不同载荷帧不相等")]
    public void Equals_DifferentPayload_ReturnsFalse()
    {
        var a = CanFrame.CreateData(StdId, [1, 2]);
        var b = CanFrame.CreateData(StdId, [1, 3]);
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同ID帧不相等")]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var id2 = CanId.Standard(0x200);
        var a = CanFrame.CreateData(StdId, [1]);
        var b = CanFrame.CreateData(id2, [1]);
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同类型帧不相等")]
    public void Equals_DifferentKind_ReturnsFalse()
    {
        var a = CanFrame.CreateData(StdId, [1]);
        var b = CanFrame.CreateRemote(StdId);
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同标志帧不相等")]
    public void Equals_DifferentFlags_ReturnsFalse()
    {
        var a = CanFrame.CreateData(StdId, [1], CanFrameFlags.None);
        var b = CanFrame.CreateFdData(StdId, [1], bitRateSwitch: true);
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同DLC帧不相等")]
    public void Equals_DifferentDlc_ReturnsFalse()
    {
        var a = CanFrame.CreateRemote(StdId, 4);
        var b = CanFrame.CreateRemote(StdId, 8);
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同错误码帧不相等")]
    public void Equals_DifferentErrorCode_ReturnsFalse()
    {
        var a = CanFrame.CreateError(1);
        var b = CanFrame.CreateError(2);
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同错误载荷帧不相等")]
    public void Equals_DifferentErrorPayload_ReturnsFalse()
    {
        var a = CanFrame.CreateError(1, [0x01]);
        var b = CanFrame.CreateError(1, [0x02]);
        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同载荷相同ID哈希不同")]
    public void DifferentPayload_SameId_DifferentHash()
    {
        var a = CanFrame.CreateData(StdId, [0, 0]);
        var b = CanFrame.CreateData(StdId, [0, 1]);
        Assert.AreNotEqual(a, b);
        Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "等号运算符比较相等")]
    public void OperatorEquals_ReturnsTrue()
    {
        var payload = new byte[] { 1, 2, 3 };
        var a = CanFrame.CreateData(StdId, payload);
        var b = CanFrame.CreateData(StdId, payload);
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    #endregion

    #region Extended ID

    [TestMethod(DisplayName = "扩展ID数据帧创建成功")]
    public void CreateData_ExtendedId_Succeeds()
    {
        var frame = CanFrame.CreateData(ExtId, [1]);
        Assert.AreEqual(ExtId, frame.Id);
        Assert.IsTrue(frame.Id.IsExtended);
    }

    [TestMethod(DisplayName = "同值标准帧与扩展帧不等")]
    public void StandardAndExtendedSameValue_AreDifferentGroupingKeys()
    {
        var standard = CanId.Standard(0x100);
        var extended = CanId.Extended(0x100);
        Assert.AreNotEqual(standard, extended);
    }

    #endregion

    #region Payload Access

    [TestMethod(DisplayName = "经典帧载荷长度属性正确")]
    public void PayloadLength_ClassicFrame_ReturnsLength()
    {
        var frame = CanFrame.CreateData(StdId, [1, 2, 3]);
        Assert.AreEqual(3, frame.PayloadLength);
        Assert.AreEqual(frame.Length, frame.PayloadLength);
    }

    [TestMethod(DisplayName = "经典帧逐字节读取载荷正确")]
    public void GetPayloadByte_ClassicFrame_ReturnsExactByte()
    {
        byte[] payload = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22];
        var frame = CanFrame.CreateData(StdId, payload);
        for (int i = 0; i < 8; i++)
            Assert.AreEqual(payload[i], frame.GetPayloadByte(i));
    }

    [TestMethod(DisplayName = "超出范围读取载荷字节抛异常")]
    public void GetPayloadByte_OutOfRange_Throws()
    {
        var frame = CanFrame.CreateData(StdId, [1, 2]);
        TestAssert.Throws<ArgumentOutOfRangeException>(() => frame.GetPayloadByte(2));
        TestAssert.Throws<ArgumentOutOfRangeException>(() => frame.GetPayloadByte(-1));
    }

    [TestMethod(DisplayName = "经典帧复制载荷字节正确")]
    public void CopyPayloadTo_ClassicFrame_CopiesExactBytes()
    {
        byte[] payload = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22];
        var frame = CanFrame.CreateData(StdId, payload);
        var dest = new byte[8];
        frame.CopyPayloadTo(dest);
        for (int i = 0; i < 8; i++)
            Assert.AreEqual(payload[i], dest[i]);
    }

    [TestMethod(DisplayName = "目标缓冲区过小抛异常")]
    public void CopyPayloadTo_DestinationTooSmall_Throws()
    {
        var frame = CanFrame.CreateData(StdId, [1, 2, 3]);
        var dest = new byte[2];
        TestAssert.Throws<ArgumentOutOfRangeException>(() => frame.CopyPayloadTo(dest));
    }

    [TestMethod(DisplayName = "空间足够复制载荷返回true")]
    public void TryCopyPayloadTo_SufficientSpace_ReturnsTrue()
    {
        byte[] payload = [0xAA, 0xBB, 0xCC];
        var frame = CanFrame.CreateData(StdId, payload);
        var dest = new byte[8];
        Assert.IsTrue(frame.TryCopyPayloadTo(dest, out int written));
        Assert.AreEqual(3, written);
        Assert.AreEqual(0xAA, dest[0]);
        Assert.AreEqual(0xBB, dest[1]);
        Assert.AreEqual(0xCC, dest[2]);
    }

    [TestMethod(DisplayName = "空间不足复制载荷返回false")]
    public void TryCopyPayloadTo_InsufficientSpace_ReturnsFalse()
    {
        var frame = CanFrame.CreateData(StdId, [1, 2, 3]);
        var dest = new byte[2];
        Assert.IsFalse(frame.TryCopyPayloadTo(dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod(DisplayName = "空载荷不执行复制")]
    public void CopyPayloadTo_EmptyPayload_NoCopy()
    {
        var frame = CanFrame.CreateData(StdId, ReadOnlySpan<byte>.Empty);
        var dest = new byte[4];
        frame.CopyPayloadTo(dest);
        Assert.AreEqual(0, dest[0]);
    }

    [TestMethod(DisplayName = "远程帧载荷为空")]
    public void CopyPayloadTo_RemoteFrame_EmptyPayload()
    {
        var frame = CanFrame.CreateRemote(StdId, 4);
        Assert.AreEqual(0, frame.PayloadLength);
    }

    #endregion

    #region Allocation Test

    [TestMethod(DisplayName = "经典帧创建零堆内存分配")]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void CreateData_NoHeapAllocation()
    {
        // Warmup
        _ = CanFrame.CreateData(StdId, [1, 2, 3]);

        long before = System.GC.GetAllocatedBytesForCurrentThread();
        Span<byte> data = stackalloc byte[8];
        data[0] = 0xFF;
        var frame = CanFrame.CreateData(StdId, data);
        long after = System.GC.GetAllocatedBytesForCurrentThread();

        Assert.AreEqual(0xFF, frame.GetPayloadByte(0));
        Assert.AreEqual(8, frame.Length);
        Assert.AreEqual(before, after, "Classic CAN frame creation should not allocate heap memory.");
    }

    [TestMethod(DisplayName = "FD帧创建零堆内存分配")]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void CreateFdData_NoHeapAllocation()
    {
        // Warmup
        _ = CanFrame.CreateFdData(StdId, [1, 2, 3]);

        long before = System.GC.GetAllocatedBytesForCurrentThread();
        Span<byte> data = stackalloc byte[64];
        data[63] = 0x42;
        var frame = CanFrame.CreateFdData(StdId, data);
        long after = System.GC.GetAllocatedBytesForCurrentThread();

        Assert.AreEqual(0x42, frame.GetPayloadByte(63));
        Assert.AreEqual(64, frame.Length);
        Assert.AreEqual(before, after, "CAN FD frame creation should not allocate heap memory.");
    }

    #endregion
}
