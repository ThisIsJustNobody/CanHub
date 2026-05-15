using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanTransmitSubmissionResultTests
{
    [TestMethod(DisplayName = "成功接收结果")]
    public void AcceptedResult_SetsAccepted()
    {
        var result = CanTransmitSubmissionResult.AcceptedResult(42);
        Assert.AreEqual(42ul, result.CorrelationId);
        Assert.AreEqual(CanTransmitSubmissionStatus.Accepted, result.Status);
        Assert.IsTrue(result.Accepted);
    }

    [TestMethod(DisplayName = "成功接收含原生状态码")]
    public void AcceptedResult_WithNativeCodes()
    {
        var result = CanTransmitSubmissionResult.AcceptedResult(1, nativeStatusCode: 10, nativeErrorCode: 20);
        Assert.AreEqual(10u, result.NativeStatusCode);
        Assert.AreEqual(20u, result.NativeErrorCode);
    }

    [TestMethod(DisplayName = "队列满失败")]
    public void Failed_QueueFull()
    {
        var result = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.QueueFull);
        Assert.AreEqual(1ul, result.CorrelationId);
        Assert.AreEqual(CanTransmitSubmissionStatus.QueueFull, result.Status);
        Assert.IsFalse(result.Accepted);
    }

    [TestMethod(DisplayName = "无效帧失败")]
    public void Failed_InvalidFrame()
    {
        var result = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.InvalidFrame);
        Assert.AreEqual(CanTransmitSubmissionStatus.InvalidFrame, result.Status);
        Assert.IsFalse(result.Accepted);
    }

    [TestMethod(DisplayName = "未启动失败")]
    public void Failed_NotStarted()
    {
        var result = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.NotStarted);
        Assert.IsFalse(result.Accepted);
    }

    [TestMethod(DisplayName = "总线关闭失败")]
    public void Failed_BusOff()
    {
        var result = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.BusOff);
        Assert.IsFalse(result.Accepted);
    }

    [TestMethod(DisplayName = "不支持的功能失败")]
    public void Failed_UnsupportedFeature()
    {
        var result = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.UnsupportedFeature);
        Assert.IsFalse(result.Accepted);
    }

    [TestMethod(DisplayName = "取消失败")]
    public void Failed_Canceled()
    {
        var result = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.Canceled);
        Assert.IsFalse(result.Accepted);
    }

    [TestMethod(DisplayName = "超时失败")]
    public void Failed_Timeout()
    {
        var result = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.Timeout);
        Assert.IsFalse(result.Accepted);
    }

    [TestMethod(DisplayName = "原生错误失败")]
    public void Failed_NativeError()
    {
        var result = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.NativeError,
            nativeStatusCode: 5, nativeErrorCode: 10);
        Assert.AreEqual(5u, result.NativeStatusCode);
        Assert.AreEqual(10u, result.NativeErrorCode);
        Assert.IsFalse(result.Accepted);
    }

    [TestMethod(DisplayName = "Accepted 状态失败抛异常")]
    public void Failed_AcceptedStatus_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.Accepted));
    }

    [TestMethod(DisplayName = "None 状态失败抛异常")]
    public void Failed_NoneStatus_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.None));
    }

    [TestMethod(DisplayName = "相同值相等")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = CanTransmitSubmissionResult.AcceptedResult(1, 10, 20);
        var b = CanTransmitSubmissionResult.AcceptedResult(1, 10, 20);
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "不同状态不相等")]
    public void Equals_DifferentStatus_ReturnsFalse()
    {
        var a = CanTransmitSubmissionResult.AcceptedResult(1);
        var b = CanTransmitSubmissionResult.Failed(1, CanTransmitSubmissionStatus.QueueFull);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同关联ID不相等")]
    public void Equals_DifferentCorrelationId_ReturnsFalse()
    {
        var a = CanTransmitSubmissionResult.AcceptedResult(1);
        var b = CanTransmitSubmissionResult.AcceptedResult(2);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "运算符 == 相等")]
    public void OperatorEquals_ReturnsTrue()
    {
        var a = CanTransmitSubmissionResult.AcceptedResult(1);
        var b = CanTransmitSubmissionResult.AcceptedResult(1);
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }
}
