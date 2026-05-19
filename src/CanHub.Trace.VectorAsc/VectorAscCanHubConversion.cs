namespace CanHub.Trace.VectorAsc;

/// <summary>
/// CanHub 帧事件与 Vector ASC 帧记录之间的转换辅助。<br/>
/// Conversion helpers between CanHub frame events and Vector ASC frame records.
/// </summary>
public static class VectorAscCanHubConversion
{
    /// <summary>
    /// 从 CanHub 帧事件创建 ASC 帧记录。<br/>
    /// Creates an ASC frame record from a CanHub frame event.
    /// </summary>
    public static VectorAscFrame FromFrameEvent(CanFrameEvent frameEvent)
    {
        var timestamp = frameEvent.HasDeviceTimestamp
            ? TimeSpan.FromSeconds((double)frameEvent.DeviceTimestampTicks / frameEvent.DeviceTimestampFrequency)
            : TimeSpan.Zero;

        return new VectorAscFrame
        {
            Sequence = frameEvent.Sequence,
            Timestamp = timestamp,
            ChannelIndex = frameEvent.ChannelIndex,
            Direction = frameEvent.Direction,
            ObservationKind = frameEvent.ObservationKind,
            EventFlags = frameEvent.EventFlags,
            Frame = frameEvent.Frame,
            SystemTimestampUtc = frameEvent.SystemTimestampUtc,
            DeviceTimestampTicks = frameEvent.DeviceTimestampTicks,
            DeviceTimestampFrequency = frameEvent.DeviceTimestampFrequency,
            DeviceTimestampKind = frameEvent.DeviceTimestampKind,
            CorrelationId = frameEvent.CorrelationId,
            Outcome = frameEvent.Outcome,
            AttemptCount = frameEvent.AttemptCount,
            NativeStatusCode = frameEvent.NativeStatusCode,
            NativeErrorCode = frameEvent.NativeErrorCode,
            IsCanFdLine = frameEvent.Frame.Flags.HasFlag(CanFrameFlags.FD)
        };
    }

    /// <summary>
    /// 从 ASC 帧记录创建 CanHub 帧事件。<br/>
    /// Creates a CanHub frame event from an ASC frame record.
    /// </summary>
    public static CanFrameEvent ToFrameEvent(VectorAscFrame record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var deviceTimestampTicks = record.DeviceTimestampFrequency > 0
            ? record.DeviceTimestampTicks
            : record.Timestamp.Ticks;
        var deviceTimestampFrequency = record.DeviceTimestampFrequency > 0
            ? record.DeviceTimestampFrequency
            : TimeSpan.TicksPerSecond;
        var deviceTimestampKind = record.DeviceTimestampFrequency > 0
            ? record.DeviceTimestampKind
            : CanTimestampKind.Relative;

        if (record.Direction == CanFrameDirection.Receive)
        {
            return CanFrameEvent.CreateReceived(
                record.Frame,
                record.Sequence,
                record.ChannelIndex,
                record.SystemTimestampUtc,
                deviceTimestampTicks,
                deviceTimestampFrequency,
                deviceTimestampKind,
                record.EventFlags,
                record.ObservationKind,
                record.NativeStatusCode,
                record.NativeErrorCode);
        }

        if (record.ObservationKind is CanFrameObservationKind.TxSubmit or CanFrameObservationKind.TxAccepted)
        {
            return CanFrameEvent.CreateTransmitSubmission(
                record.CorrelationId,
                record.Frame,
                accepted: record.ObservationKind == CanFrameObservationKind.TxAccepted,
                record.Sequence,
                record.ChannelIndex,
                record.SystemTimestampUtc,
                deviceTimestampTicks,
                deviceTimestampFrequency,
                deviceTimestampKind,
                record.NativeStatusCode,
                record.NativeErrorCode);
        }

        var outcome = record.Outcome;
        if (outcome == CanTransmitOutcome.None)
        {
            outcome = record.ObservationKind == CanFrameObservationKind.TxFailed
                ? CanTransmitOutcome.Failed
                : CanTransmitOutcome.Transmitted;
        }

        return CanFrameEvent.CreateTransmitted(
            record.CorrelationId,
            record.Frame,
            outcome,
            record.Sequence,
            record.ChannelIndex,
            record.SystemTimestampUtc,
            deviceTimestampTicks,
            deviceTimestampFrequency,
            deviceTimestampKind,
            Math.Max(1, record.AttemptCount),
            record.NativeStatusCode,
            record.NativeErrorCode);
    }
}
