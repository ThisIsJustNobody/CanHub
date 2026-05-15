using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanFrameEventTests
{
    private static CanFrame CreateSampleFrame() =>
        CanFrame.CreateData(CanId.Standard(0x100), [1, 2, 3]);

    #region Receive Events

    [TestMethod(DisplayName = "接收事件默认来源为总线")]
    public void CreateReceived_DefaultsSourceToBus()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), sequence: 1);
        Assert.AreEqual(CanFrameObservationKind.Bus, evt.ObservationKind);
    }

    [TestMethod(DisplayName = "接收事件设置本地回显")]
    public void CreateReceived_SetsObservationKindLocalEcho()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), sequence: 1,
            observationKind: CanFrameObservationKind.LocalEcho);
        Assert.AreEqual(CanFrameObservationKind.LocalEcho, evt.ObservationKind);
    }

    [TestMethod(DisplayName = "接收事件设置自发自收")]
    public void CreateReceived_SetsObservationKindSelfReception()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), sequence: 1,
            observationKind: CanFrameObservationKind.SelfReception);
        Assert.AreEqual(CanFrameObservationKind.SelfReception, evt.ObservationKind);
    }

    [TestMethod(DisplayName = "接收事件设置驱动回显")]
    public void CreateReceived_SetsObservationKindDriverEcho()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), sequence: 1,
            observationKind: CanFrameObservationKind.DriverEcho);
        Assert.AreEqual(CanFrameObservationKind.DriverEcho, evt.ObservationKind);
    }

    [TestMethod(DisplayName = "接收事件使用发送观察类型时抛出异常")]
    public void CreateReceived_TxObservationKind_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrameEvent.CreateReceived(CreateSampleFrame(), sequence: 1,
                observationKind: CanFrameObservationKind.TxConfirmed));
    }

    [TestMethod(DisplayName = "接收事件通道索引为逻辑事件源值")]
    public void CreateReceived_ChannelIndex_IsLogicalEventSourceValue()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0, channelIndex: 12);
        Assert.AreEqual(12, evt.ChannelIndex);
    }

    [TestMethod(DisplayName = "负数通道索引时抛出异常")]
    public void CreateReceived_NegativeChannelIndex_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrameEvent.CreateReceived(CreateSampleFrame(), 0, channelIndex: -1));
    }

    [TestMethod(DisplayName = "系统时间戳默认为当前UTC时间")]
    public void CreateReceived_SystemTimestampUtc_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0);
        var after = DateTimeOffset.UtcNow;
        Assert.IsGreaterThanOrEqualTo(before.AddSeconds(-1), evt.SystemTimestampUtc);
        Assert.IsLessThanOrEqualTo(after.AddSeconds(1), evt.SystemTimestampUtc);
    }

    [TestMethod(DisplayName = "系统时间戳可指定值")]
    public void CreateReceived_SystemTimestampUtc_Specified()
    {
        var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0, systemTimestampUtc: ts);
        Assert.AreEqual(ts, evt.SystemTimestampUtc);
    }

    [TestMethod(DisplayName = "接收事件带设备时间戳")]
    public void CreateReceived_DeviceTimestamp()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0,
            deviceTimestampTicks: 123456,
            deviceTimestampFrequency: 1000000,
            deviceTimestampKind: CanTimestampKind.Relative);
        Assert.AreEqual(123456L, evt.DeviceTimestampTicks);
        Assert.AreEqual(1000000L, evt.DeviceTimestampFrequency);
        Assert.AreEqual(CanTimestampKind.Relative, evt.DeviceTimestampKind);
        Assert.IsTrue(evt.HasDeviceTimestamp);
    }

    [TestMethod(DisplayName = "无设备时间戳时HasDeviceTimestamp为假")]
    public void CreateReceived_NoDeviceTimestamp_HasDeviceTimestampFalse()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0);
        Assert.IsFalse(evt.HasDeviceTimestamp);
    }

    [TestMethod(DisplayName = "接收事件带原生错误码")]
    public void CreateReceived_NativeCodes()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0,
            nativeStatusCode: 10, nativeErrorCode: 20);
        Assert.AreEqual(10u, evt.NativeStatusCode);
        Assert.AreEqual(20u, evt.NativeErrorCode);
    }

    [TestMethod(DisplayName = "接收事件事件标志正确设置")]
    public void CreateReceived_EventFlags()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0,
            eventFlags: CanFrameEventFlags.Filtered | CanFrameEventFlags.Loopback);
        Assert.IsTrue(evt.EventFlags.HasFlag(CanFrameEventFlags.Filtered));
        Assert.IsTrue(evt.EventFlags.HasFlag(CanFrameEventFlags.Loopback));
        Assert.IsFalse(evt.EventFlags.HasFlag(CanFrameEventFlags.ErrorResponse));
    }

    #endregion

    #region Transmit Events

    [TestMethod(DisplayName = "创建Transmitted事件设置正确属性")]
    public void CreateTransmitted_SetsTransmittedOutcome()
    {
        var evt = CanFrameEvent.CreateTransmitted(42, CreateSampleFrame(), CanTransmitOutcome.Transmitted);
        Assert.AreEqual(42ul, evt.CorrelationId);
        Assert.AreEqual(CanTransmitOutcome.Transmitted, evt.Outcome);
        Assert.AreEqual(CanFrameDirection.Transmit, evt.Direction);
        Assert.AreEqual(CreateSampleFrame(), evt.Frame);
    }

    [TestMethod(DisplayName = "创建Transmitted事件带重试次数")]
    public void CreateTransmitted_WithAttemptCount()
    {
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted, attemptCount: 3);
        Assert.AreEqual(3, evt.AttemptCount);
    }

    [TestMethod(DisplayName = "创建Transmitted事件结果为失败")]
    public void CreateTransmitted_FailedOutcome()
    {
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Failed);
        Assert.AreEqual(CanTransmitOutcome.Failed, evt.Outcome);
        Assert.AreEqual(CanFrameObservationKind.TxFailed, evt.ObservationKind);
    }

    [TestMethod(DisplayName = "结果为None时抛出异常")]
    public void CreateTransmitted_NoneOutcome_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.None));
    }

    [TestMethod(DisplayName = "重试次数为负数时抛出异常")]
    public void CreateTransmitted_NegativeAttemptCount_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted, attemptCount: -1));
    }

    [TestMethod(DisplayName = "重试次数为零时抛出异常")]
    public void CreateTransmitted_ZeroAttemptCount_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted, attemptCount: 0));
    }

    [TestMethod(DisplayName = "负数通道索引时抛出异常")]
    public void CreateTransmitted_NegativeChannelIndex_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted, channelIndex: -1));
    }

    [TestMethod(DisplayName = "提交事件被接受")]
    public void CreateTransmitSubmission_Accepted()
    {
        var evt = CanFrameEvent.CreateTransmitSubmission(1, CreateSampleFrame(), accepted: true);
        Assert.AreEqual(CanFrameDirection.Transmit, evt.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxAccepted, evt.ObservationKind);
        Assert.AreEqual(CanTransmitOutcome.None, evt.Outcome);
    }

    [TestMethod(DisplayName = "提交事件被拒绝")]
    public void CreateTransmitSubmission_Rejected()
    {
        var evt = CanFrameEvent.CreateTransmitSubmission(1, CreateSampleFrame(), accepted: false);
        Assert.AreEqual(CanFrameDirection.Transmit, evt.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxSubmit, evt.ObservationKind);
    }

    [TestMethod(DisplayName = "提交事件完整参数覆盖")]
    public void CreateTransmitSubmission_AllParameters_Stored()
    {
        var frame = CreateSampleFrame();
        var ts = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var evt = CanFrameEvent.CreateTransmitSubmission(
            correlationId: 42,
            frame: frame,
            accepted: true,
            sequence: 99,
            channelIndex: 3,
            systemTimestampUtc: ts,
            deviceTimestampTicks: 500000,
            deviceTimestampFrequency: 1000000,
            deviceTimestampKind: CanTimestampKind.Relative,
            nativeStatusCode: 7,
            nativeErrorCode: 8);

        Assert.AreEqual(42ul, evt.CorrelationId);
        Assert.AreEqual(frame, evt.Frame);
        Assert.AreEqual(CanFrameDirection.Transmit, evt.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxAccepted, evt.ObservationKind);
        Assert.AreEqual(99ul, evt.Sequence);
        Assert.AreEqual(3, evt.ChannelIndex);
        Assert.AreEqual(ts, evt.SystemTimestampUtc);
        Assert.AreEqual(500000L, evt.DeviceTimestampTicks);
        Assert.AreEqual(1000000L, evt.DeviceTimestampFrequency);
        Assert.AreEqual(CanTimestampKind.Relative, evt.DeviceTimestampKind);
        Assert.IsTrue(evt.HasDeviceTimestamp);
        Assert.AreEqual(7u, evt.NativeStatusCode);
        Assert.AreEqual(8u, evt.NativeErrorCode);
    }

    [TestMethod(DisplayName = "提交事件负数通道索引抛出异常")]
    public void CreateTransmitSubmission_NegativeChannelIndex_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanFrameEvent.CreateTransmitSubmission(1, CreateSampleFrame(), accepted: true, channelIndex: -1));
    }

    #endregion

    #region Default Values

    [TestMethod(DisplayName = "默认方向为None")]
    public void Default_Direction_IsNone()
    {
        var evt = default(CanFrameEvent);
        Assert.AreEqual(CanFrameDirection.None, evt.Direction);
    }

    [TestMethod(DisplayName = "默认观察类型为None")]
    public void Default_ObservationKind_IsNone()
    {
        var evt = default(CanFrameEvent);
        Assert.AreEqual(CanFrameObservationKind.None, evt.ObservationKind);
    }

    [TestMethod(DisplayName = "默认结果为None")]
    public void Default_Outcome_IsNone()
    {
        var evt = default(CanFrameEvent);
        Assert.AreEqual(CanTransmitOutcome.None, evt.Outcome);
    }

    #endregion

    #region Absent Properties

    #endregion

    #region Equality

    [TestMethod(DisplayName = "相同属性值的事件相等")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var frame = CreateSampleFrame();
        var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = CanFrameEvent.CreateReceived(frame, 1, 0, ts, 100, 1000,
            CanTimestampKind.Absolute, CanFrameEventFlags.None,
            CanFrameObservationKind.LocalEcho, 0, 0);
        var b = CanFrameEvent.CreateReceived(frame, 1, 0, ts, 100, 1000,
            CanTimestampKind.Absolute, CanFrameEventFlags.None,
            CanFrameObservationKind.LocalEcho, 0, 0);
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod(DisplayName = "不同观察类型事件不相等")]
    public void Equals_DifferentObservationKind_ReturnsFalse()
    {
        var frame = CreateSampleFrame();
        var a = CanFrameEvent.CreateReceived(frame, 1, observationKind: CanFrameObservationKind.Bus);
        var b = CanFrameEvent.CreateReceived(frame, 1, observationKind: CanFrameObservationKind.LocalEcho);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同序列号事件不相等")]
    public void Equals_DifferentSequence_ReturnsFalse()
    {
        var frame = CreateSampleFrame();
        var a = CanFrameEvent.CreateReceived(frame, 1);
        var b = CanFrameEvent.CreateReceived(frame, 2);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同帧事件不相等")]
    public void Equals_DifferentFrame_ReturnsFalse()
    {
        var f1 = CanFrame.CreateData(CanId.Standard(0x100), [1]);
        var f2 = CanFrame.CreateData(CanId.Standard(0x100), [2]);
        var a = CanFrameEvent.CreateReceived(f1, 1);
        var b = CanFrameEvent.CreateReceived(f2, 1);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同通道事件不相等")]
    public void Equals_DifferentChannel_ReturnsFalse()
    {
        var frame = CreateSampleFrame();
        var a = CanFrameEvent.CreateReceived(frame, 1, channelIndex: 0);
        var b = CanFrameEvent.CreateReceived(frame, 1, channelIndex: 1);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同原生状态码事件不相等")]
    public void Equals_DifferentNativeStatus_ReturnsFalse()
    {
        var frame = CreateSampleFrame();
        var a = CanFrameEvent.CreateReceived(frame, 1, nativeStatusCode: 0);
        var b = CanFrameEvent.CreateReceived(frame, 1, nativeStatusCode: 1);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同方向事件不相等")]
    public void Equals_DifferentDirection_ReturnsFalse()
    {
        var frame = CreateSampleFrame();
        var a = CanFrameEvent.CreateReceived(frame, 1);
        var b = CanFrameEvent.CreateTransmitted(1, frame, CanTransmitOutcome.Transmitted);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同结果事件不相等")]
    public void Equals_DifferentOutcome_ReturnsFalse()
    {
        var frame = CreateSampleFrame();
        var a = CanFrameEvent.CreateTransmitted(1, frame, CanTransmitOutcome.Transmitted);
        var b = CanFrameEvent.CreateTransmitted(1, frame, CanTransmitOutcome.Failed);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "相等事件运算符正确")]
    public void OperatorEquals_ReturnsTrue()
    {
        var frame = CreateSampleFrame();
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = CanFrameEvent.CreateReceived(frame, 1, systemTimestampUtc: timestamp);
        var b = CanFrameEvent.CreateReceived(frame, 1, systemTimestampUtc: timestamp);
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod(DisplayName = "默认设备时间戳类型为绝对时间")]
    public void DeviceTimestampKind_Default_IsAbsolute()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0);
        Assert.AreEqual(CanTimestampKind.Absolute, evt.DeviceTimestampKind);
    }

    [TestMethod(DisplayName = "序列号可以为零")]
    public void Sequence_CanBeZero()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0);
        Assert.AreEqual(0ul, evt.Sequence);
    }

    [TestMethod(DisplayName = "默认事件标志为None")]
    public void EventFlags_Default_IsNone()
    {
        var evt = CanFrameEvent.CreateReceived(CreateSampleFrame(), 0);
        Assert.AreEqual(CanFrameEventFlags.None, evt.EventFlags);
    }

    #endregion
}
