using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanEndpointTests
{
    #region Basic Parsing

    [TestMethod(DisplayName = "解析虚拟端点正确提取各字段")]
    public void Parse_VirtualEndpoint_ParsesSchemeDeviceChannelAndParameters()
    {
        var ep = CanEndpoint.Parse("virtual://bench?channel=0&fd=true&bitrate=500000");

        Assert.AreEqual("virtual", ep.Scheme);
        Assert.AreEqual("bench", ep.Device);
        Assert.AreEqual(0, ep.Channel);
        Assert.HasCount(2, ep.Parameters);
        Assert.AreEqual("true", ep.Parameters["fd"]);
        Assert.AreEqual("500000", ep.Parameters["bitrate"]);
    }

    [TestMethod(DisplayName = "无通道时参数不含channel")]
    public void Parse_NoChannel_ParametersDoNotContainChannel()
    {
        var ep = CanEndpoint.Parse("virtual://bench?fd=true&bitrate=500000");

        Assert.AreEqual("virtual", ep.Scheme);
        Assert.AreEqual("bench", ep.Device);
        Assert.IsNull(ep.Channel);
        Assert.HasCount(2, ep.Parameters);
        Assert.AreEqual("true", ep.Parameters["fd"]);
    }

    [TestMethod(DisplayName = "无查询参数仅设备名")]
    public void Parse_NoQuery_DeviceOnly()
    {
        var ep = CanEndpoint.Parse("vector://ch0");

        Assert.AreEqual("vector", ep.Scheme);
        Assert.AreEqual("ch0", ep.Device);
        Assert.IsNull(ep.Channel);
        Assert.IsEmpty(ep.Parameters);
    }

    #endregion

    #region Scheme Canonicalization

    [TestMethod(DisplayName = "方案名被规范化为小写")]
    public void Parse_SchemeCanonicalizedToLowercase()
    {
        var ep = CanEndpoint.Parse("VIRTUAL://bench?channel=0");

        Assert.AreEqual("virtual", ep.Scheme);
        Assert.AreEqual("bench", ep.Device);
    }

    [TestMethod(DisplayName = "混合大小写方案保留设备大小写")]
    public void Parse_SchemeMixedCase_PreservesDeviceCasing()
    {
        var ep = CanEndpoint.Parse("ZlgCan://MyDevice?channel=2");

        Assert.AreEqual("zlgcan", ep.Scheme);
        Assert.AreEqual("MyDevice", ep.Device);
    }

    #endregion

    #region Invalid Inputs - Throw CanException

    [TestMethod(DisplayName = "空输入抛出CanException")]
    public void Parse_NullInput_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse(null!));
    }

    [TestMethod(DisplayName = "空字符串输入抛出CanException")]
    public void Parse_EmptyInput_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse(""));
    }

    [TestMethod(DisplayName = "空白输入抛出CanException")]
    public void Parse_WhitespaceInput_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("   "));
    }

    [TestMethod(DisplayName = "无效URI格式抛出CanException")]
    public void Parse_InvalidUriFormat_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("not a uri at all"));
    }

    [TestMethod(DisplayName = "缺少双斜杠的URI抛出CanException")]
    public void Parse_MissingAuthoritySeparator_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual:bench?channel=0"));
    }

    [TestMethod(DisplayName = "设备名为空抛出CanException")]
    public void Parse_EmptyDevice_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://?channel=0"));
    }

    [TestMethod(DisplayName = "包含片段标识符抛出CanException")]
    public void Parse_FragmentPresent_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://bench?channel=0#1"));
    }

    [TestMethod(DisplayName = "通道为负数抛出CanException")]
    public void Parse_NegativeChannel_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://bench?channel=-1"));
    }

    [TestMethod(DisplayName = "通道非整数抛出CanException")]
    public void Parse_NonIntegerChannel_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://bench?channel=abc"));
    }

    [TestMethod(DisplayName = "重复查询参数抛出CanException")]
    public void Parse_DuplicateQueryParam_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://bench?channel=0&channel=1"));
    }

    [TestMethod(DisplayName = "无效查询转义抛出CanException")]
    public void Parse_InvalidQueryEscape_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://bench?name=%"));
    }

    [TestMethod(DisplayName = "包含路径时抛出CanException")]
    public void Parse_PathPresent_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://bench/path?channel=0"));
    }

    [TestMethod(DisplayName = "包含用户信息时抛出CanException")]
    public void Parse_UserInfoPresent_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://user@bench?channel=0"));
    }

    [TestMethod(DisplayName = "包含端口时抛出CanException")]
    public void Parse_PortPresent_ThrowsCanException()
    {
        TestAssert.Throws<CanException>(() => CanEndpoint.Parse("virtual://bench:1?channel=0"));
    }

    #endregion

    #region Real-World Endpoints

    [TestMethod(DisplayName = "解析ZLG端点正确")]
    public void Parse_ZlgEndpoint_ParsesCorrectly()
    {
        var ep = CanEndpoint.Parse("zlgcan://USB0?channel=0&bitrate=500000&fdBitrate=2000000");

        Assert.AreEqual("zlgcan", ep.Scheme);
        Assert.AreEqual("USB0", ep.Device);
        Assert.AreEqual(0, ep.Channel);
        Assert.AreEqual("500000", ep.Parameters["bitrate"]);
        Assert.AreEqual("2000000", ep.Parameters["fdBitrate"]);
    }

    [TestMethod(DisplayName = "解析Vector端点正确")]
    public void Parse_VectorEndpoint_ParsesCorrectly()
    {
        var ep = CanEndpoint.Parse("vector://VN1610?channel=3");

        Assert.AreEqual("vector", ep.Scheme);
        Assert.AreEqual("VN1610", ep.Device);
        Assert.AreEqual(3, ep.Channel);
        Assert.IsEmpty(ep.Parameters);
    }

    #endregion

    #region Equality

    [TestMethod(DisplayName = "相同值的端点相等")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = CanEndpoint.Parse("virtual://bench?channel=0&fd=true");
        var b = CanEndpoint.Parse("VIRTUAL://bench?channel=0&fd=true");

        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同设备名的端点不相等")]
    public void Equals_DifferentDevice_ReturnsFalse()
    {
        var a = CanEndpoint.Parse("virtual://bench1?channel=0");
        var b = CanEndpoint.Parse("virtual://bench2?channel=0");

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同方案的端点不相等")]
    public void Equals_DifferentScheme_ReturnsFalse()
    {
        var a = CanEndpoint.Parse("virtual://bench?channel=0");
        var b = CanEndpoint.Parse("vector://bench?channel=0");

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同参数的端点不相等")]
    public void Equals_DifferentParameters_ReturnsFalse()
    {
        var a = CanEndpoint.Parse("virtual://bench?channel=0&fd=true");
        var b = CanEndpoint.Parse("virtual://bench?channel=0&fd=false");

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "不同通道的端点不相等")]
    public void Equals_DifferentChannel_ReturnsFalse()
    {
        var a = CanEndpoint.Parse("virtual://bench?channel=0");
        var b = CanEndpoint.Parse("virtual://bench?channel=1");

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod(DisplayName = "相等端点的哈希码相同")]
    public void GetHashCode_EqualEndpoints_SameHashCodes()
    {
        var a = CanEndpoint.Parse("virtual://bench?channel=0&fd=true");
        var b = CanEndpoint.Parse("VIRTUAL://bench?channel=0&fd=true");

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "查询参数名大小写不同的相等端点哈希码相同")]
    public void GetHashCode_EqualEndpointsWithDifferentParameterCasing_SameHashCodes()
    {
        var a = CanEndpoint.Parse("virtual://bench?channel=0&fd=true");
        var b = CanEndpoint.Parse("virtual://bench?channel=0&FD=true");

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "相等端点的等于运算符返回true")]
    public void Equals_Operator_EqualEndpoints_ReturnsTrue()
    {
        var a = CanEndpoint.Parse("virtual://bench?channel=0");
        var b = CanEndpoint.Parse("VIRTUAL://bench?channel=0");

        Assert.IsTrue(a == b);
    }

    [TestMethod(DisplayName = "不同端点的等于运算符返回false")]
    public void Equals_Operator_DifferentEndpoints_ReturnsFalse()
    {
        var a = CanEndpoint.Parse("virtual://bench?channel=0");
        var b = CanEndpoint.Parse("vector://bench?channel=0");

        Assert.IsFalse(a == b);
    }

    #endregion

    #region ToString Round-Trip

    [TestMethod(DisplayName = "ToString输出可被Parse还原")]
    public void ToString_RoundTrip_PreservesFields()
    {
        var original = "virtual://bench?channel=0&fd=true&bitrate=500000";
        var ep = CanEndpoint.Parse(original);
        var output = ep.ToString();

        var reparsed = CanEndpoint.Parse(output);
        Assert.AreEqual(ep.Scheme, reparsed.Scheme);
        Assert.AreEqual(ep.Device, reparsed.Device);
        Assert.AreEqual(ep.Channel, reparsed.Channel);
        Assert.AreEqual(ep.Parameters.Count, reparsed.Parameters.Count);
        foreach (var p in ep.Parameters)
            Assert.AreEqual(p.Value, reparsed.Parameters[p.Key]);
    }

    [TestMethod(DisplayName = "无参数端点ToString简洁")]
    public void ToString_NoQuery_NoTrailingQuestionMark()
    {
        var ep = CanEndpoint.Parse("vector://VN1610");
        Assert.AreEqual("vector://VN1610", ep.ToString());
    }

    [TestMethod(DisplayName = "参数按键名排序")]
    public void ToString_ParametersSorted()
    {
        var ep = CanEndpoint.Parse("virtual://bench?channel=0&zebra=a&alpha=b");
        var output = ep.ToString();
        Assert.AreEqual("virtual://bench?channel=0&alpha=b&zebra=a", output);
    }

    #endregion
}
