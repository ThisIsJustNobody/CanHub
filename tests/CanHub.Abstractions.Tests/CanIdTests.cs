using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanIdTests
{
    [TestMethod(DisplayName = "标准帧有效值构造成功")]
    [DataRow(0u)]
    [DataRow(0x7FFu)]
    public void StandardId_ValidValues_Succeeds(uint value)
    {
        var id = new CanId(value);
        Assert.AreEqual(value, id.Value);
        Assert.IsFalse(id.IsExtended);
    }

    [TestMethod(DisplayName = "标准帧超范围抛出异常")]
    [DataRow(0x800u)]
    [DataRow(0xFFFu)]
    [DataRow(uint.MaxValue)]
    public void StandardId_ExceedsMax_Throws(uint value)
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new CanId(value));
    }

    [TestMethod(DisplayName = "扩展帧有效值构造成功")]
    [DataRow(0u)]
    [DataRow(0x1FFFFFFFu)]
    public void ExtendedId_ValidValues_Succeeds(uint value)
    {
        var id = new CanId(value, true);
        Assert.AreEqual(value, id.Value);
        Assert.IsTrue(id.IsExtended);
    }

    [TestMethod(DisplayName = "扩展帧超范围抛出异常")]
    [DataRow(0x20000000u)]
    [DataRow(uint.MaxValue)]
    public void ExtendedId_ExceedsMax_Throws(uint value)
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new CanId(value, true));
    }

    [TestMethod(DisplayName = "标准帧工厂方法创建成功")]
    public void Standard_FactoryMethod_CreatesStandardId()
    {
        var id = CanId.Standard(0x100);
        Assert.AreEqual(0x100u, id.Value);
        Assert.IsFalse(id.IsExtended);
    }

    [TestMethod(DisplayName = "扩展帧工厂方法创建成功")]
    public void Extended_FactoryMethod_CreatesExtendedId()
    {
        var id = CanId.Extended(0x1000);
        Assert.AreEqual(0x1000u, id.Value);
        Assert.IsTrue(id.IsExtended);
    }

    [TestMethod(DisplayName = "CanId相等性值语义测试")]
    public void CanId_Equality_ValueSemantics()
    {
        var a = CanId.Standard(42);
        var b = CanId.Standard(42);
        var c = CanId.Standard(43);
        var d = CanId.Extended(42);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.AreNotEqual(a, c);
        Assert.AreNotEqual(a, d);
    }

    #region ToString

    [TestMethod(DisplayName = "ToString格式化输出正确")]
    [DataRow(0x100u, false, "0x100")]
    [DataRow(0x0u, false, "0x0")]
    [DataRow(0x7FFu, false, "0x7FF")]
    [DataRow(0x100000u, true, "0x100000x")]
    [DataRow(0x1FFFFFFFu, true, "0x1FFFFFFFx")]
    public void ToString_FormatsCorrectly(uint value, bool extended, string expected)
    {
        var id = new CanId(value, extended);
        Assert.AreEqual(expected, id.ToString());
    }

    #endregion

    #region Parse

    [TestMethod(DisplayName = "Parse有效输入解析成功")]
    [DataRow("0x100", 0x100u, false)]
    [DataRow("0x0", 0x0u, false)]
    [DataRow("0x7FF", 0x7FFu, false)]
    [DataRow("0x100000x", 0x100000u, true)]
    [DataRow("0x1FFFFFFFx", 0x1FFFFFFFu, true)]
    public void Parse_ValidInputs_Succeeds(string text, uint expectedValue, bool expectedExtended)
    {
        var id = CanId.Parse(text);
        Assert.AreEqual(expectedValue, id.Value);
        Assert.AreEqual(expectedExtended, id.IsExtended);
    }

    [TestMethod(DisplayName = "Parse无效输入抛出异常")]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("0x")]
    [DataRow("100")]
    [DataRow("0x100000")]
    [DataRow("0x20000000x")]
    public void Parse_InvalidInputs_Throws(string text)
    {
        TestAssert.Throws<FormatException>(() => CanId.Parse(text));
    }

    #endregion

    #region TryParse

    [TestMethod(DisplayName = "TryParse有效输入返回True")]
    [DataRow("0x100", 0x100u, false)]
    [DataRow("0x0", 0x0u, false)]
    [DataRow("0x7FF", 0x7FFu, false)]
    [DataRow("0x100000x", 0x100000u, true)]
    [DataRow("0x1FFFFFFFx", 0x1FFFFFFFu, true)]
    public void TryParse_ValidInputs_ReturnsTrue(string text, uint expectedValue, bool expectedExtended)
    {
        Assert.IsTrue(CanId.TryParse(text, out var id));
        Assert.AreEqual(expectedValue, id.Value);
        Assert.AreEqual(expectedExtended, id.IsExtended);
    }

    [TestMethod(DisplayName = "TryParse无效输入返回False")]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("0x")]
    [DataRow("100")]
    [DataRow("0x100000")]
    [DataRow("0x20000000x")]
    public void TryParse_InvalidInputs_ReturnsFalse(string text)
    {
        Assert.IsFalse(CanId.TryParse(text, out var id));
        Assert.AreEqual(default(CanId), id);
    }

    [TestMethod(DisplayName = "TryParse大小写不敏感")]
    public void TryParse_CaseInsensitive_PrefixAndSuffix()
    {
        Assert.IsTrue(CanId.TryParse("0X100", out var std));
        Assert.AreEqual(0x100u, std.Value);
        Assert.IsFalse(std.IsExtended);

        Assert.IsTrue(CanId.TryParse("0X100000X", out var ext));
        Assert.AreEqual(0x100000u, ext.Value);
        Assert.IsTrue(ext.IsExtended);
    }

    #endregion

}
