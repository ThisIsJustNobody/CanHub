using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanBusParametersTests
{
    [TestMethod(DisplayName = "Classic500k 预设：非 FD，仲裁波特率 500k")]
    public void Classic500k_IsNotFd_500kbps()
    {
        var p = CanBusParameters.Classic500k;
        Assert.IsFalse(p.IsFd);
        Assert.AreEqual(500_000, p.ArbitrationBitrate);
        Assert.IsNull(p.DataBitrate);
    }

    [TestMethod(DisplayName = "Fd500k2M 预设：FD 模式，仲裁 500k，数据 2M")]
    public void Fd500k2M_IsFd_500kArb_2MData()
    {
        var p = CanBusParameters.Fd500k2M;
        Assert.IsTrue(p.IsFd);
        Assert.AreEqual(500_000, p.ArbitrationBitrate);
        Assert.AreEqual(2_000_000, p.DataBitrate);
    }

    [TestMethod(DisplayName = "默认构造：所有 nullable 参数为 null")]
    public void Default_NullableFieldsAreNull()
    {
        var p = new CanBusParameters { IsFd = false, ArbitrationBitrate = 500_000 };
        Assert.IsNull(p.DataBitrate);
        Assert.IsNull(p.ArbitrationTseg1);
        Assert.IsNull(p.ArbitrationTseg2);
        Assert.IsNull(p.ArbitrationSjw);
        Assert.IsNull(p.DataTseg1);
        Assert.IsNull(p.DataTseg2);
        Assert.IsNull(p.DataSjw);
        Assert.IsNull(p.TerminationEnabled);
        Assert.IsNull(p.AckOff);
        Assert.IsNull(p.SelfAck);
    }

    [TestMethod(DisplayName = "IsNonIsoFd 默认为 false")]
    public void Default_IsNonIsoFd_IsFalse()
    {
        var p = new CanBusParameters { IsFd = true, ArbitrationBitrate = 500_000 };
        Assert.IsFalse(p.IsNonIsoFd);
    }

    [TestMethod(DisplayName = "相同值的两个实例 Equals 返回 true")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new CanBusParameters
        {
            IsFd = true,
            ArbitrationBitrate = 500_000,
            DataBitrate = 2_000_000,
            ArbitrationTseg1 = 29,
            ArbitrationTseg2 = 10,
        };
        var b = new CanBusParameters
        {
            IsFd = true,
            ArbitrationBitrate = 500_000,
            DataBitrate = 2_000_000,
            ArbitrationTseg1 = 29,
            ArbitrationTseg2 = 10,
        };

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "不同值的两个实例 Equals 返回 false")]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var a = CanBusParameters.Classic500k;
        var b = CanBusParameters.Fd500k2M;

        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "全参数构造后所有字段可读")]
    public void AllFields_GetSet()
    {
        var p = new CanBusParameters
        {
            IsFd = true,
            ArbitrationBitrate = 1_000_000,
            DataBitrate = 4_000_000,
            IsNonIsoFd = true,
            ArbitrationTseg1 = 29,
            ArbitrationTseg2 = 10,
            ArbitrationSjw = 1,
            DataTseg1 = 14,
            DataTseg2 = 7,
            DataSjw = 2,
            TerminationEnabled = false,
            AckOff = true,
            SelfAck = false,
            UnsupportedParameterPolicy = CanBusParameterPolicy.Require,
        };

        Assert.IsTrue(p.IsFd);
        Assert.AreEqual(1_000_000, p.ArbitrationBitrate);
        Assert.AreEqual(4_000_000, p.DataBitrate);
        Assert.IsTrue(p.IsNonIsoFd);
        Assert.AreEqual(29, p.ArbitrationTseg1);
        Assert.AreEqual(10, p.ArbitrationTseg2);
        Assert.AreEqual(1, p.ArbitrationSjw);
        Assert.AreEqual(14, p.DataTseg1);
        Assert.AreEqual(7, p.DataTseg2);
        Assert.AreEqual(2, p.DataSjw);
        Assert.AreEqual(false, p.TerminationEnabled);
        Assert.AreEqual(true, p.AckOff);
        Assert.AreEqual(false, p.SelfAck);
        Assert.AreEqual(CanBusParameterPolicy.Require, p.UnsupportedParameterPolicy);
    }

    [TestMethod(DisplayName = "相同值运算符==返回true")]
    public void OperatorEquals_SameValues_ReturnsTrue()
    {
        var a = CanBusParameters.Classic500k;
        var b = CanBusParameters.Classic500k;
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod(DisplayName = "不同值运算符==返回false")]
    public void OperatorEquals_DifferentValues_ReturnsFalse()
    {
        var a = CanBusParameters.Classic500k;
        var b = CanBusParameters.Fd500k2M;
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [TestMethod(DisplayName = "null与null运算符==返回true")]
    public void OperatorEquals_NullAndNull_ReturnsTrue()
    {
        CanBusParameters? a = null;
        CanBusParameters? b = null;
        Assert.IsTrue(a == b);
    }

    [TestMethod(DisplayName = "null与非null运算符==返回false")]
    public void OperatorEquals_NullAndNonNull_ReturnsFalse()
    {
        CanBusParameters? a = null;
        CanBusParameters? b = CanBusParameters.Classic500k;
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [TestMethod(DisplayName = "Validate接受有效参数")]
    public void Validate_ValidParameters_DoesNotThrow()
    {
        CanBusParameters.Fd500k2M.Validate();
    }

    [TestMethod(DisplayName = "Validate拒绝非正仲裁波特率")]
    public void Validate_NonPositiveArbitrationBitrate_Throws()
    {
        var p = new CanBusParameters { ArbitrationBitrate = 0 };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => p.Validate());
    }

    [TestMethod(DisplayName = "Validate拒绝FD模式缺少数据段波特率")]
    public void Validate_FdWithoutDataBitrate_Throws()
    {
        var p = new CanBusParameters
        {
            IsFd = true,
            ArbitrationBitrate = 500_000,
        };

        Assert.ThrowsExactly<ArgumentNullException>(() => p.Validate());
    }

    [TestMethod(DisplayName = "Validate拒绝无效UnsupportedParameterPolicy")]
    public void Validate_InvalidUnsupportedParameterPolicy_Throws()
    {
        var p = new CanBusParameters
        {
            ArbitrationBitrate = 500_000,
            UnsupportedParameterPolicy = (CanBusParameterPolicy)99,
        };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => p.Validate());
    }
}
