using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanTransmitOptionsTests
{
    [TestMethod(DisplayName = "全参数创建选项")]
    public void Create_WithAllParameters()
    {
        var retry = CanTransmitRetryPolicy.LimitedRetries(3);
        var options = CanTransmitOptions.Create(
            mode: CanTransmitMode.SingleShot,
            completion: CanTransmitCompletion.WaitForTransmitConfirmation,
            retryPolicy: retry,
            highPriority: true);
        Assert.AreEqual(CanTransmitMode.SingleShot, options.Mode);
        Assert.AreEqual(CanTransmitCompletion.WaitForTransmitConfirmation, options.Completion);
        Assert.AreEqual(retry, options.RetryPolicy);
        Assert.IsTrue(options.HighPriority);
    }

    [TestMethod(DisplayName = "ZLG 四种发送模式")]
    public void Create_ZlgFourTransmitModes()
    {
        var normal = CanTransmitOptions.Create(mode: CanTransmitMode.Normal);
        var singleShot = CanTransmitOptions.Create(mode: CanTransmitMode.SingleShot);
        var selfReception = CanTransmitOptions.Create(mode: CanTransmitMode.SelfReception);
        var singleShotSelfReception = CanTransmitOptions.Create(mode: CanTransmitMode.SingleShotSelfReception);
        Assert.AreEqual(CanTransmitMode.Normal, normal.Mode);
        Assert.AreEqual(CanTransmitMode.SingleShot, singleShot.Mode);
        Assert.AreEqual(CanTransmitMode.SelfReception, selfReception.Mode);
        Assert.AreEqual(CanTransmitMode.SingleShotSelfReception, singleShotSelfReception.Mode);
        Assert.AreNotEqual(normal, singleShot);
        Assert.AreNotEqual(selfReception, singleShotSelfReception);
    }

    [TestMethod(DisplayName = "相同值相等")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = CanTransmitOptions.Create(mode: CanTransmitMode.Normal, highPriority: true);
        var b = CanTransmitOptions.Create(mode: CanTransmitMode.Normal, highPriority: true);
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "不同模式不相等")]
    public void Equals_DifferentMode_ReturnsFalse()
    {
        var a = CanTransmitOptions.Create(mode: CanTransmitMode.Normal);
        var b = CanTransmitOptions.Create(mode: CanTransmitMode.SingleShot);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同优先级不相等")]
    public void Equals_DifferentHighPriority_ReturnsFalse()
    {
        var a = CanTransmitOptions.Create(highPriority: false);
        var b = CanTransmitOptions.Create(highPriority: true);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "运算符 == 相等")]
    public void OperatorEquals_ReturnsTrue()
    {
        var a = CanTransmitOptions.Create(mode: CanTransmitMode.Normal);
        var b = CanTransmitOptions.Create(mode: CanTransmitMode.Normal);
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

}
