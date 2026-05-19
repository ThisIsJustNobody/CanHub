namespace CanHub.Trace.VectorAsc;

/// <summary>
/// ASC Trace 中的一条帧记录。<br/>
/// One frame record in an ASC trace.
/// </summary>
public sealed record VectorAscFrame
{
    /// <summary>文件中的 1 基行号。<br/>One-based source line number.</summary>
    public int LineNumber { get; init; }

    /// <summary>事件序列号。<br/>Event sequence number.</summary>
    public ulong Sequence { get; init; }

    /// <summary>Trace 时间戳。<br/>Trace timestamp.</summary>
    public TimeSpan Timestamp { get; init; }

    /// <summary>零基 CAN 通道索引。<br/>Zero-based CAN channel index.</summary>
    public int ChannelIndex { get; init; }

    /// <summary>帧方向。<br/>Frame direction.</summary>
    public CanFrameDirection Direction { get; init; }

    /// <summary>观察类型。<br/>Observation kind.</summary>
    public CanFrameObservationKind ObservationKind { get; init; }

    /// <summary>事件标志。<br/>Event flags.</summary>
    public CanFrameEventFlags EventFlags { get; init; }

    /// <summary>CAN 帧。<br/>CAN frame.</summary>
    public CanFrame Frame { get; init; }

    /// <summary>系统 UTC 时间戳。<br/>UTC system timestamp.</summary>
    public DateTimeOffset? SystemTimestampUtc { get; init; }

    /// <summary>设备时间戳 ticks。<br/>Device timestamp ticks.</summary>
    public long DeviceTimestampTicks { get; init; }

    /// <summary>设备时间戳频率。<br/>Device timestamp frequency.</summary>
    public long DeviceTimestampFrequency { get; init; }

    /// <summary>设备时间戳类型。<br/>Device timestamp kind.</summary>
    public CanTimestampKind DeviceTimestampKind { get; init; }

    /// <summary>发送关联 ID。<br/>Transmit correlation identifier.</summary>
    public ulong CorrelationId { get; init; }

    /// <summary>发送结果。<br/>Transmit outcome.</summary>
    public CanTransmitOutcome Outcome { get; init; }

    /// <summary>发送尝试次数。<br/>Transmit attempt count.</summary>
    public int AttemptCount { get; init; }

    /// <summary>原生状态码。<br/>Native status code.</summary>
    public uint NativeStatusCode { get; init; }

    /// <summary>原生错误码。<br/>Native error code.</summary>
    public uint NativeErrorCode { get; init; }

    /// <summary>是否来自 CANFD 行。<br/>Whether this record came from a CANFD row.</summary>
    public bool IsCanFdLine { get; init; }

    /// <summary>CAN FD 符号名称。<br/>CAN FD symbolic name.</summary>
    public string? SymbolicName { get; init; }

    /// <summary>CAN FD flags 字段。<br/>CAN FD flags field.</summary>
    public uint? CanFdFlags { get; init; }
}
