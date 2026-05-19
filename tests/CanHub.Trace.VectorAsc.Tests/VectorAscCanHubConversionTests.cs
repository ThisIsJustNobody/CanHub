using CanHub.Trace.VectorAsc;

namespace CanHub.Trace.VectorAsc.Tests;

[TestClass]
public class VectorAscCanHubConversionTests
{
    [TestMethod(DisplayName = "CanFrameEvent接收事件转换为ASC记录并回转")]
    public void FromFrameEvent_ReceiveEvent_RoundTripsMetadata()
    {
        var systemTimestamp = new DateTimeOffset(2026, 5, 19, 13, 0, 0, TimeSpan.Zero);
        var source = CanFrameEvent.CreateReceived(
            CanFrame.CreateData(CanId.Standard(0x123), [0x01, 0x02]),
            sequence: 42,
            channelIndex: 2,
            systemTimestampUtc: systemTimestamp,
            deviceTimestampTicks: 1234,
            deviceTimestampFrequency: 1000,
            deviceTimestampKind: CanTimestampKind.Relative,
            eventFlags: CanFrameEventFlags.Loopback,
            observationKind: CanFrameObservationKind.DriverEcho);

        var record = VectorAscCanHubConversion.FromFrameEvent(source);

        Assert.AreEqual(42ul, record.Sequence);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1234), record.Timestamp);
        Assert.AreEqual(2, record.ChannelIndex);
        Assert.AreEqual(CanFrameDirection.Receive, record.Direction);
        Assert.AreEqual(CanFrameObservationKind.DriverEcho, record.ObservationKind);
        Assert.AreEqual(CanFrameEventFlags.Loopback, record.EventFlags);
        Assert.AreEqual(systemTimestamp, record.SystemTimestampUtc);
        Assert.AreEqual(source.Frame, record.Frame);

        var roundTrip = VectorAscCanHubConversion.ToFrameEvent(record);

        Assert.AreEqual(source.Sequence, roundTrip.Sequence);
        Assert.AreEqual(source.Frame, roundTrip.Frame);
        Assert.AreEqual(source.Direction, roundTrip.Direction);
        Assert.AreEqual(source.ObservationKind, roundTrip.ObservationKind);
        Assert.AreEqual(source.ChannelIndex, roundTrip.ChannelIndex);
        Assert.AreEqual(source.SystemTimestampUtc, roundTrip.SystemTimestampUtc);
        Assert.AreEqual(source.DeviceTimestampTicks, roundTrip.DeviceTimestampTicks);
        Assert.AreEqual(source.DeviceTimestampFrequency, roundTrip.DeviceTimestampFrequency);
        Assert.AreEqual(source.DeviceTimestampKind, roundTrip.DeviceTimestampKind);
        Assert.AreEqual(source.EventFlags, roundTrip.EventFlags);
    }

    [TestMethod(DisplayName = "CanFrameEvent发送事件转换为ASC记录并回转")]
    public void FromFrameEvent_TransmitEvent_RoundTripsMetadata()
    {
        var source = CanFrameEvent.CreateTransmitted(
            correlationId: 77,
            CanFrame.CreateData(CanId.Standard(0x321), [0xAA]),
            CanTransmitOutcome.Transmitted,
            sequence: 9,
            channelIndex: 1,
            systemTimestampUtc: new DateTimeOffset(2026, 5, 19, 13, 0, 0, TimeSpan.Zero),
            deviceTimestampTicks: 2500,
            deviceTimestampFrequency: 1000,
            deviceTimestampKind: CanTimestampKind.Relative,
            attemptCount: 2,
            nativeStatusCode: 0x10,
            nativeErrorCode: 0x20);

        var record = VectorAscCanHubConversion.FromFrameEvent(source);

        Assert.AreEqual(77ul, record.CorrelationId);
        Assert.AreEqual(CanFrameDirection.Transmit, record.Direction);
        Assert.AreEqual(CanFrameObservationKind.TxConfirmed, record.ObservationKind);
        Assert.AreEqual(CanTransmitOutcome.Transmitted, record.Outcome);
        Assert.AreEqual(2, record.AttemptCount);
        Assert.AreEqual(0x10u, record.NativeStatusCode);
        Assert.AreEqual(0x20u, record.NativeErrorCode);

        var roundTrip = VectorAscCanHubConversion.ToFrameEvent(record);

        Assert.AreEqual(source.Sequence, roundTrip.Sequence);
        Assert.AreEqual(source.CorrelationId, roundTrip.CorrelationId);
        Assert.AreEqual(source.Frame, roundTrip.Frame);
        Assert.AreEqual(source.Direction, roundTrip.Direction);
        Assert.AreEqual(source.ObservationKind, roundTrip.ObservationKind);
        Assert.AreEqual(source.Outcome, roundTrip.Outcome);
        Assert.AreEqual(source.AttemptCount, roundTrip.AttemptCount);
        Assert.AreEqual(source.NativeStatusCode, roundTrip.NativeStatusCode);
        Assert.AreEqual(source.NativeErrorCode, roundTrip.NativeErrorCode);
    }
}
