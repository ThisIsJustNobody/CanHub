using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanStatusEventTests
{
    [TestMethod(DisplayName = "默认状态事件各字段均为None")]
    public void Default_IsNotInitialized()
    {
        var evt = default(CanStatusEvent);
        Assert.AreEqual(CanStatusKind.None, evt.Kind);
        Assert.AreEqual(CanStatusCode.None, evt.Code);
        Assert.AreEqual(CanStatusSeverity.None, evt.Severity);
        Assert.IsFalse(evt.IsInitialized);
    }

    [TestMethod(DisplayName = "创建丢帧状态事件")]
    public void Create_DroppedFrames_Status()
    {
        var evt = CanStatusEvent.Create(
            CanStatusKind.Subscription,
            CanStatusCode.DroppedFrames,
            CanStatusSeverity.Warning,
            sequence: 10,
            channelIndex: 2,
            relatedFrameSequence: 9,
            count: 3);

        Assert.IsTrue(evt.IsInitialized);
        Assert.AreEqual(CanStatusKind.Subscription, evt.Kind);
        Assert.AreEqual(CanStatusCode.DroppedFrames, evt.Code);
        Assert.AreEqual(CanStatusSeverity.Warning, evt.Severity);
        Assert.AreEqual(2, evt.ChannelIndex);
        Assert.AreEqual(9ul, evt.RelatedFrameSequence);
        Assert.AreEqual(3ul, evt.Count);
    }

    [TestMethod(DisplayName = "创建队列压力状态事件")]
    public void Create_QueuePressure_Status()
    {
        var evt = CanStatusEvent.Create(
            CanStatusKind.Subscription,
            CanStatusCode.QueuePressure,
            CanStatusSeverity.Warning,
            channelIndex: 1,
            count: 90);

        Assert.AreEqual(CanStatusCode.QueuePressure, evt.Code);
        Assert.AreEqual(90ul, evt.Count);
    }

    [TestMethod(DisplayName = "创建总线关闭状态事件")]
    public void Create_BusOff_Status()
    {
        var evt = CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.BusOff,
            CanStatusSeverity.Critical,
            channelIndex: 1,
            nativeStatusCode: 0x10,
            nativeErrorCode: 0x20);

        Assert.AreEqual(CanStatusCode.BusOff, evt.Code);
        Assert.AreEqual(CanStatusSeverity.Critical, evt.Severity);
        Assert.AreEqual(0x10u, evt.NativeStatusCode);
        Assert.AreEqual(0x20u, evt.NativeErrorCode);
    }

    [TestMethod(DisplayName = "创建原生驱动错误状态事件")]
    public void Create_NativeDriverError_Status()
    {
        var evt = CanStatusEvent.Create(
            CanStatusKind.Driver,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            nativeStatusCode: 5,
            nativeErrorCode: 10);

        Assert.AreEqual(CanStatusKind.Driver, evt.Kind);
        Assert.AreEqual(CanStatusCode.NativeDriverError, evt.Code);
        Assert.AreEqual(5u, evt.NativeStatusCode);
        Assert.AreEqual(10u, evt.NativeErrorCode);
    }

    [TestMethod(DisplayName = "创建带关联ID的状态事件")]
    public void Create_WithCorrelationId()
    {
        var evt = CanStatusEvent.Create(
            CanStatusKind.Transmit,
            CanStatusCode.QueueFull,
            CanStatusSeverity.Warning,
            correlationId: 42);

        Assert.AreEqual(42ul, evt.CorrelationId);
    }

    [TestMethod(DisplayName = "Kind为None时抛出异常")]
    public void Create_NoneKind_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanStatusEvent.Create(CanStatusKind.None, CanStatusCode.Started, CanStatusSeverity.Info));
    }

    [TestMethod(DisplayName = "Code为None时抛出异常")]
    public void Create_NoneCode_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanStatusEvent.Create(CanStatusKind.Device, CanStatusCode.None, CanStatusSeverity.Info));
    }

    [TestMethod(DisplayName = "Severity为None时抛出异常")]
    public void Create_NoneSeverity_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanStatusEvent.Create(CanStatusKind.Device, CanStatusCode.Started, CanStatusSeverity.None));
    }

    [TestMethod(DisplayName = "通道索引为负时抛出异常")]
    public void Create_NegativeChannelIndex_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanStatusEvent.Create(
                CanStatusKind.Channel,
                CanStatusCode.Disconnected,
                CanStatusSeverity.Warning,
                channelIndex: -1));
    }

    [TestMethod(DisplayName = "相同值的事件相等")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = CanStatusEvent.Create(
            CanStatusKind.Driver,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            sequence: 1,
            channelIndex: 2,
            systemTimestampUtc: timestamp,
            nativeStatusCode: 3,
            nativeErrorCode: 4);
        var b = CanStatusEvent.Create(
            CanStatusKind.Driver,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            sequence: 1,
            channelIndex: 2,
            systemTimestampUtc: timestamp,
            nativeStatusCode: 3,
            nativeErrorCode: 4);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "Create 不传 message 参数时 Message 为 null")]
    public void Create_NoMessage_MessageIsNull()
    {
        var evt = CanStatusEvent.Create(
            CanStatusKind.Device,
            CanStatusCode.Started,
            CanStatusSeverity.Info);

        Assert.IsNull(evt.Message);
    }

    [TestMethod(DisplayName = "Create 传入 message 后 Message 可读")]
    public void Create_WithMessage_MessageIsSaved()
    {
        var evt = CanStatusEvent.Create(
            CanStatusKind.Channel,
            CanStatusCode.ConfigurationIgnored,
            CanStatusSeverity.Warning,
            message: "参数 'SamplePoint' 被忽略: 适配器不支持");

        Assert.AreEqual("参数 'SamplePoint' 被忽略: 适配器不支持", evt.Message);
    }

    [TestMethod(DisplayName = "Message 不同时 Equals 返回 false")]
    public void Equals_DifferentMessage_ReturnsFalse()
    {
        var a = CanStatusEvent.Create(
            CanStatusKind.Channel,
            CanStatusCode.Started,
            CanStatusSeverity.Info,
            message: "A");
        var b = CanStatusEvent.Create(
            CanStatusKind.Channel,
            CanStatusCode.Started,
            CanStatusSeverity.Info,
            message: "B");

        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "Message 相同时 Equals 返回 true")]
    public void Equals_SameMessage_ReturnsTrue()
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = CanStatusEvent.Create(
            CanStatusKind.Channel,
            CanStatusCode.Started,
            CanStatusSeverity.Info,
            systemTimestampUtc: timestamp,
            message: "test");
        var b = CanStatusEvent.Create(
            CanStatusKind.Channel,
            CanStatusCode.Started,
            CanStatusSeverity.Info,
            systemTimestampUtc: timestamp,
            message: "test");

        Assert.AreEqual(a, b);
    }

    [TestMethod(DisplayName = "Message 不同时 GetHashCode 不同")]
    public void GetHashCode_DifferentMessage_DifferentHash()
    {
        var a = CanStatusEvent.Create(
            CanStatusKind.Channel,
            CanStatusCode.Started,
            CanStatusSeverity.Info,
            message: "A");
        var b = CanStatusEvent.Create(
            CanStatusKind.Channel,
            CanStatusCode.Started,
            CanStatusSeverity.Info,
            message: "B");

        Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "ConfigurationIgnored 工厂生成 Warning + Channel + ConfigurationIgnored")]
    public void ConfigurationIgnored_ProducesWarningEvent()
    {
        var evt = CanStatusEvent.ConfigurationIgnored(
            "SamplePoint", "适配器不支持此配置", channelIndex: 3);

        Assert.AreEqual(CanStatusKind.Channel, evt.Kind);
        Assert.AreEqual(CanStatusCode.ConfigurationIgnored, evt.Code);
        Assert.AreEqual(CanStatusSeverity.Warning, evt.Severity);
        Assert.AreEqual(3, evt.ChannelIndex);
        Assert.IsTrue(evt.Message!.Contains("SamplePoint"));
        Assert.IsTrue(evt.Message!.Contains("适配器不支持此配置"));
    }
}
