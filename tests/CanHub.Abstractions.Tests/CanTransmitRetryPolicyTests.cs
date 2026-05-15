using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanTransmitRetryPolicyTests
{
    [TestMethod(DisplayName = "默认策略应为 None")]
    public void None_Default_HasKindNone()
    {
        var policy = default(CanTransmitRetryPolicy);
        Assert.AreEqual(CanTransmitRetryKind.None, policy.Kind);
        Assert.AreEqual(0, policy.MaxRetryCount);
        Assert.AreEqual(TimeSpan.Zero, policy.Timeout);
    }

    [TestMethod(DisplayName = "NoRetry 返回 None")]
    public void NoRetry_ReturnsNone()
    {
        var policy = CanTransmitRetryPolicy.NoRetry();
        Assert.AreEqual(CanTransmitRetryKind.None, policy.Kind);
        Assert.AreEqual(0, policy.MaxRetryCount);
        Assert.AreEqual(TimeSpan.Zero, policy.Timeout);
    }

    [TestMethod(DisplayName = "零次重试表示仅试一次")]
    public void LimitedRetries_Zero_ExplicitTryOnce()
    {
        var policy = CanTransmitRetryPolicy.LimitedRetries(0);
        Assert.AreEqual(CanTransmitRetryKind.LimitedRetries, policy.Kind);
        Assert.AreEqual(0, policy.MaxRetryCount);
    }

    [TestMethod(DisplayName = "设置重试次数")]
    public void LimitedRetries_SetCount()
    {
        var policy = CanTransmitRetryPolicy.LimitedRetries(5);
        Assert.AreEqual(CanTransmitRetryKind.LimitedRetries, policy.Kind);
        Assert.AreEqual(5, policy.MaxRetryCount);
    }

    [TestMethod(DisplayName = "负数重试次数抛异常")]
    public void LimitedRetries_NegativeCount_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanTransmitRetryPolicy.LimitedRetries(-1));
    }

    [TestMethod(DisplayName = "设置超时时间")]
    public void UntilTimeout_SetTimeout()
    {
        var timeout = TimeSpan.FromSeconds(2);
        var policy = CanTransmitRetryPolicy.UntilTimeout(timeout);
        Assert.AreEqual(CanTransmitRetryKind.UntilTimeout, policy.Kind);
        Assert.AreEqual(timeout, policy.Timeout);
    }

    [TestMethod(DisplayName = "零超时抛异常")]
    public void UntilTimeout_ZeroTimeout_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanTransmitRetryPolicy.UntilTimeout(TimeSpan.Zero));
    }

    [TestMethod(DisplayName = "负超时抛异常")]
    public void UntilTimeout_NegativeTimeout_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanTransmitRetryPolicy.UntilTimeout(TimeSpan.FromSeconds(-1)));
    }

    [TestMethod(DisplayName = "UntilCanceled 类型正确")]
    public void UntilCanceled_HasKind()
    {
        var policy = CanTransmitRetryPolicy.UntilCanceled();
        Assert.AreEqual(CanTransmitRetryKind.UntilCanceled, policy.Kind);
    }

    [TestMethod(DisplayName = "组合策略设置值")]
    public void LimitedRetriesOrTimeout_SetValues()
    {
        var timeout = TimeSpan.FromSeconds(3);
        var policy = CanTransmitRetryPolicy.LimitedRetriesOrTimeout(10, timeout);
        Assert.AreEqual(CanTransmitRetryKind.LimitedRetriesOrTimeout, policy.Kind);
        Assert.AreEqual(10, policy.MaxRetryCount);
        Assert.AreEqual(timeout, policy.Timeout);
    }

    [TestMethod(DisplayName = "组合策略负次数抛异常")]
    public void LimitedRetriesOrTimeout_NegativeCount_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanTransmitRetryPolicy.LimitedRetriesOrTimeout(-1, TimeSpan.FromSeconds(1)));
    }

    [TestMethod(DisplayName = "组合策略零超时抛异常")]
    public void LimitedRetriesOrTimeout_ZeroTimeout_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanTransmitRetryPolicy.LimitedRetriesOrTimeout(5, TimeSpan.Zero));
    }

    [TestMethod(DisplayName = "Unlimited 类型正确")]
    public void Unlimited_HasKind()
    {
        var policy = CanTransmitRetryPolicy.Unlimited();
        Assert.AreEqual(CanTransmitRetryKind.Unlimited, policy.Kind);
    }

    [TestMethod(DisplayName = "相同值相等")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = CanTransmitRetryPolicy.LimitedRetries(3);
        var b = CanTransmitRetryPolicy.LimitedRetries(3);
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "不同类型不相等")]
    public void Equals_DifferentKind_ReturnsFalse()
    {
        var a = CanTransmitRetryPolicy.NoRetry();
        var b = CanTransmitRetryPolicy.LimitedRetries(3);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同次数不相等")]
    public void Equals_DifferentCount_ReturnsFalse()
    {
        var a = CanTransmitRetryPolicy.LimitedRetries(1);
        var b = CanTransmitRetryPolicy.LimitedRetries(2);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同超时不相等")]
    public void Equals_DifferentTimeout_ReturnsFalse()
    {
        var a = CanTransmitRetryPolicy.UntilTimeout(TimeSpan.FromSeconds(1));
        var b = CanTransmitRetryPolicy.UntilTimeout(TimeSpan.FromSeconds(2));
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "运算符 == 相等")]
    public void OperatorEquals_ReturnsTrue()
    {
        var a = CanTransmitRetryPolicy.NoRetry();
        var b = CanTransmitRetryPolicy.NoRetry();
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }
}
