using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

/// <summary>
/// 发送端事件的扩展覆盖：各 Outcome 枚举值、时间戳、关联 ID 差异化相等性。
/// 基础 CreateTransmitted 测试见 CanFrameEventTests。
/// </summary>
[TestClass]
public class CanFrameTransmitEventTests
{
    private static CanFrame CreateSampleFrame() =>
        CanFrame.CreateData(CanId.Standard(0x100), [1, 2, 3]);

    #region Outcome Variants

    [TestMethod(DisplayName = "非Transmitted结果映射为TxFailed观察类型")]
    public void CreateTransmitted_NonTransmittedOutcome_MapsToTxFailed()
    {
        var outcomes = new[]
        {
            CanTransmitOutcome.Canceled,
            CanTransmitOutcome.TimedOut,
            CanTransmitOutcome.BusOff,
            CanTransmitOutcome.UnsupportedFeature,
            CanTransmitOutcome.ConfirmationUnavailable,
            CanTransmitOutcome.NativeError,
            CanTransmitOutcome.Failed
        };
        foreach (var outcome in outcomes)
        {
            var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), outcome);
            Assert.AreEqual(outcome, evt.Outcome,
                $"Outcome {outcome} should be preserved.");
            Assert.AreEqual(CanFrameObservationKind.TxFailed, evt.ObservationKind,
                $"Outcome {outcome} should map to TxFailed observation kind.");
        }
    }

    [TestMethod(DisplayName = "创建Transmitted事件带原生错误码")]
    public void CreateTransmitted_NativeErrorOutcome()
    {
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.NativeError,
            nativeStatusCode: 7, nativeErrorCode: 8);
        Assert.AreEqual(7u, evt.NativeStatusCode);
        Assert.AreEqual(8u, evt.NativeErrorCode);
    }

    #endregion

    #region Device Timestamp

    [TestMethod(DisplayName = "创建Transmitted事件带设备时间戳")]
    public void CreateTransmitted_DeviceTimestamp()
    {
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted,
            deviceTimestampTicks: 500000,
            deviceTimestampFrequency: 1000000,
            deviceTimestampKind: CanTimestampKind.Relative);
        Assert.AreEqual(500000L, evt.DeviceTimestampTicks);
        Assert.AreEqual(1000000L, evt.DeviceTimestampFrequency);
        Assert.AreEqual(CanTimestampKind.Relative, evt.DeviceTimestampKind);
        Assert.IsTrue(evt.HasDeviceTimestamp);
    }

    [TestMethod(DisplayName = "无设备时间戳时HasDeviceTimestamp为假")]
    public void CreateTransmitted_NoDeviceTimestamp_HasDeviceTimestampFalse()
    {
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted);
        Assert.IsFalse(evt.HasDeviceTimestamp);
    }

    [TestMethod(DisplayName = "系统时间戳默认为当前UTC时间")]
    public void CreateTransmitted_SystemTimestampUtc_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted);
        var after = DateTimeOffset.UtcNow;
        Assert.IsGreaterThanOrEqualTo(before.AddSeconds(-1), evt.SystemTimestampUtc);
        Assert.IsLessThanOrEqualTo(after.AddSeconds(1), evt.SystemTimestampUtc);
    }

    [TestMethod(DisplayName = "系统时间戳可指定值")]
    public void CreateTransmitted_SystemTimestampUtc_Specified()
    {
        var ts = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted, systemTimestampUtc: ts);
        Assert.AreEqual(ts, evt.SystemTimestampUtc);
    }

    #endregion

    #region Equality (unique fields)

    [TestMethod(DisplayName = "不同关联ID事件不相等")]
    public void Equals_DifferentCorrelationId_ReturnsFalse()
    {
        var a = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted);
        var b = CanFrameEvent.CreateTransmitted(2, CreateSampleFrame(), CanTransmitOutcome.Transmitted);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod(DisplayName = "不同重试次数事件不相等")]
    public void Equals_DifferentAttemptCount_ReturnsFalse()
    {
        var a = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted, attemptCount: 1);
        var b = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted, attemptCount: 2);
        Assert.AreNotEqual(a, b);
    }

    #endregion

    #region Observation Kind Derived from Outcome

    [TestMethod(DisplayName = "Transmitted事件观察类型为TxConfirmed")]
    public void CreateTransmitted_HasTxConfirmedObservationKind()
    {
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Transmitted);
        Assert.AreEqual(CanFrameObservationKind.TxConfirmed, evt.ObservationKind);
    }

    [TestMethod(DisplayName = "失败Transmitted事件观察类型为TxFailed")]
    public void CreateTransmitted_HasTxFailedObservationKind()
    {
        var evt = CanFrameEvent.CreateTransmitted(1, CreateSampleFrame(), CanTransmitOutcome.Failed);
        Assert.AreEqual(CanFrameObservationKind.TxFailed, evt.ObservationKind);
    }

    #endregion
}
