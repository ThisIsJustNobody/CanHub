namespace CanHub;

/// <summary>
/// 帧方向（接收 vs 发送）。<br/>
/// Frame direction (receive vs. transmit).
/// </summary>
public enum CanFrameDirection : byte
{
    /// <summary>未指定方向。<br/>No direction specified.</summary>
    None = 0,

    /// <summary>帧已接收。<br/>Frame was received.</summary>
    Receive = 1,

    /// <summary>帧已（或正在）发送。<br/>Frame was (or is being) transmitted.</summary>
    Transmit = 2
}

/// <summary>
/// CAN 帧事件的观察类型，足以区分所有跟踪时间线位置。<br/>
/// Observation kind for a CAN frame event, sufficient to distinguish all trace timeline positions.
/// </summary>
public enum CanFrameObservationKind : byte
{
    /// <summary>未指定观察类型。<br/>No observation kind specified.</summary>
    None = 0,

    /// <summary>从网络接收的总线帧。<br/>Bus reception from the network.</summary>
    Bus = 1,

    /// <summary>已提交帧的本地接收路径回显。<br/>Local receive-path echo of a submitted frame.</summary>
    LocalEcho = 2,

    /// <summary>设备/硬件通过接收路径自收发送的帧。<br/>Device/hardware self-reception of a transmitted frame through receive path.</summary>
    SelfReception = 3,

    /// <summary>驱动提供的接收路径回显。<br/>Driver-provided receive-path echo.</summary>
    DriverEcho = 4,

    /// <summary>帧已提交到驱动队列。<br/>Frame submitted to the driver queue.</summary>
    TxSubmit = 5,

    /// <summary>驱动已将帧接受到其队列中。<br/>Driver accepted the frame into its queue.</summary>
    TxAccepted = 6,

    /// <summary>控制器确认在总线上发送成功。<br/>Controller confirmed successful transmission on the bus.</summary>
    TxConfirmed = 7,

    /// <summary>发送失败。<br/>Transmission failed.</summary>
    TxFailed = 8
}

/// <summary>
/// 设备时间戳类型。<br/>
/// Device timestamp kind.
/// </summary>
public enum CanTimestampKind : byte
{
    /// <summary>绝对时间。<br/>Absolute time.</summary>
    Absolute = 0,

    /// <summary>相对时间（自设备启动起）。<br/>Relative time (since device boot).</summary>
    Relative = 1
}

/// <summary>
/// CAN 帧事件标志。<br/>
/// CAN frame event flags.
/// </summary>
[Flags]
public enum CanFrameEventFlags : byte
{
    /// <summary>无特殊标志。<br/>No special flags.</summary>
    None = 0,

    /// <summary>帧被过滤器拒绝。<br/>Frame was rejected by a filter.</summary>
    Filtered = 1 << 0,

    /// <summary>回环帧。<br/>Loopback frame.</summary>
    Loopback = 1 << 1,

    /// <summary>对错误帧的错误响应。<br/>Error response to an error frame.</summary>
    ErrorResponse = 1 << 2
}

/// <summary>
/// 统一的 CAN 帧事件。只读结构体，用于接收和发送帧的跟踪时间线排序。<br/>
/// Unified CAN frame event. Readonly struct for trace timeline ordering of both received and transmitted frames.
/// </summary>
public readonly struct CanFrameEvent : IEquatable<CanFrameEvent>
{
    /// <summary>序列号，用于排序/注解关联。<br/>Sequence number for ordering / annotation association.</summary>
    public ulong Sequence { get; }

    /// <summary>CAN 帧。<br/>CAN frame.</summary>
    public CanFrame Frame { get; }

    /// <summary>帧方向（接收或发送）。<br/>Frame direction (receive or transmit).</summary>
    public CanFrameDirection Direction { get; }

    /// <summary>观察类型（总线、回显、TX 提交、TX 确认等）。<br/>Observation kind (bus, echo, TX submit, TX confirm, etc.).</summary>
    public CanFrameObservationKind ObservationKind { get; }

    /// <summary>与发送请求匹配的关联标识。接收事件为 0。<br/>Correlation identifier matching the transmit request. 0 for receive events.</summary>
    public ulong CorrelationId { get; }

    /// <summary>
    /// 事件创建时捕获的应用程序分配逻辑通道索引。这不是供应商驱动通道号；更改所属 CanHub 实例索引仅影响未来的事件。<br/>
    /// Application-assigned logical channel index captured when this event is created.
    /// This is not a vendor driver channel number; changing the owning CanHub instance index affects only future events.
    /// </summary>
    public int ChannelIndex { get; }

    /// <summary>系统时间戳（UTC）。<br/>System timestamp (UTC).</summary>
    public DateTimeOffset SystemTimestampUtc { get; }

    /// <summary>设备时间戳（原始 ticks）。<br/>Device timestamp (raw ticks).</summary>
    public long DeviceTimestampTicks { get; }

    /// <summary>设备时间戳频率（Hz）。<br/>Device timestamp frequency (Hz).</summary>
    public long DeviceTimestampFrequency { get; }

    /// <summary>设备时间戳类型。<br/>Device timestamp kind.</summary>
    public CanTimestampKind DeviceTimestampKind { get; }

    /// <summary>是否存在设备时间戳（频率大于 0）。<br/>Whether a device timestamp is present (frequency &gt; 0).</summary>
    public bool HasDeviceTimestamp => DeviceTimestampFrequency > 0;

    /// <summary>帧事件标志（主要用于接收事件）。<br/>Frame event flags (primarily for receive events).</summary>
    public CanFrameEventFlags EventFlags { get; }

    /// <summary>发送结果。接收事件为 <see cref="CanTransmitOutcome.None"/>。<br/>Transmit outcome. <see cref="CanTransmitOutcome.None"/> for receive events.</summary>
    public CanTransmitOutcome Outcome { get; }

    /// <summary>发送尝试次数（>= 0）。接收事件为 0。<br/>Number of transmit attempts (>= 0). 0 for receive events.</summary>
    public int AttemptCount { get; }

    /// <summary>驱动返回的原始状态码。<br/>Native status code from the driver.</summary>
    public uint NativeStatusCode { get; }

    /// <summary>驱动返回的原始错误码。<br/>Native error code from the driver.</summary>
    public uint NativeErrorCode { get; }

    private CanFrameEvent(
        ulong sequence, CanFrame frame,
        CanFrameDirection direction, CanFrameObservationKind observationKind,
        ulong correlationId,
        int channelIndex, DateTimeOffset systemTimestampUtc,
        long deviceTimestampTicks, long deviceTimestampFrequency,
        CanTimestampKind deviceTimestampKind,
        CanFrameEventFlags eventFlags,
        CanTransmitOutcome outcome, int attemptCount,
        uint nativeStatusCode, uint nativeErrorCode)
    {
        ValidateChannelIndex(channelIndex);
        Sequence = sequence;
        Frame = frame;
        Direction = direction;
        ObservationKind = observationKind;
        CorrelationId = correlationId;
        ChannelIndex = channelIndex;
        SystemTimestampUtc = systemTimestampUtc;
        DeviceTimestampTicks = deviceTimestampTicks;
        DeviceTimestampFrequency = deviceTimestampFrequency;
        DeviceTimestampKind = deviceTimestampKind;
        EventFlags = eventFlags;
        Outcome = outcome;
        AttemptCount = attemptCount;
        NativeStatusCode = nativeStatusCode;
        NativeErrorCode = nativeErrorCode;
    }

    /// <summary>
    /// 创建接收帧事件。会验证 observationKind 必须是接收类（Bus/LocalEcho/SelfReception/DriverEcho）。<br/>
    /// Create a received frame event. Validates that observationKind is a receive kind (Bus/LocalEcho/SelfReception/DriverEcho).
    /// </summary>
    public static CanFrameEvent CreateReceived(
        CanFrame frame,
        ulong sequence,
        int channelIndex = 0,
        DateTimeOffset? systemTimestampUtc = null,
        long deviceTimestampTicks = 0,
        long deviceTimestampFrequency = 0,
        CanTimestampKind deviceTimestampKind = default,
        CanFrameEventFlags eventFlags = default,
        CanFrameObservationKind observationKind = CanFrameObservationKind.Bus,
        uint nativeStatusCode = 0,
        uint nativeErrorCode = 0)
    {
        if (observationKind is CanFrameObservationKind.None or
            CanFrameObservationKind.TxSubmit or
            CanFrameObservationKind.TxAccepted or
            CanFrameObservationKind.TxConfirmed or
            CanFrameObservationKind.TxFailed)
            throw new ArgumentOutOfRangeException(nameof(observationKind), observationKind,
                "Receive events must use Bus, LocalEcho, SelfReception, or DriverEcho observation kinds.");

        return new(sequence, frame,
            CanFrameDirection.Receive, observationKind,
            0, channelIndex,
            systemTimestampUtc ?? DateTimeOffset.UtcNow,
            deviceTimestampTicks, deviceTimestampFrequency,
            deviceTimestampKind, eventFlags,
            CanTransmitOutcome.None, 0,
            nativeStatusCode, nativeErrorCode);
    }

    /// <summary>
    /// 创建发送帧事件。要求 outcome 不能为 None，且 attemptCount 至少为 1。observationKind 由 outcome 自动推导。<br/>
    /// Create a transmitted frame event. Requires outcome not be None and at least one attempt. The observationKind is automatically derived from the outcome.
    /// </summary>
    public static CanFrameEvent CreateTransmitted(
        ulong correlationId,
        CanFrame frame,
        CanTransmitOutcome outcome,
        ulong sequence = 0,
        int channelIndex = 0,
        DateTimeOffset? systemTimestampUtc = null,
        long deviceTimestampTicks = 0,
        long deviceTimestampFrequency = 0,
        CanTimestampKind deviceTimestampKind = default,
        int attemptCount = 1,
        uint nativeStatusCode = 0,
        uint nativeErrorCode = 0)
    {
        if (outcome == CanTransmitOutcome.None)
            throw new ArgumentOutOfRangeException(nameof(outcome),
                "Outcome must not be None for a transmit event.");
        if (attemptCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptCount), attemptCount,
                "Final transmit events must have at least one attempt.");
        var observationKind = outcome == CanTransmitOutcome.Transmitted
            ? CanFrameObservationKind.TxConfirmed
            : CanFrameObservationKind.TxFailed;
        return new(sequence, frame,
            CanFrameDirection.Transmit, observationKind,
            correlationId, channelIndex,
            systemTimestampUtc ?? DateTimeOffset.UtcNow,
            deviceTimestampTicks, deviceTimestampFrequency,
            deviceTimestampKind, CanFrameEventFlags.None,
            outcome, attemptCount,
            nativeStatusCode, nativeErrorCode);
    }

    /// <summary>创建发送提交/接受事件。accepted 参数决定 observationKind 为 TxAccepted 还是 TxSubmit。<br/>Create a transmit submit/accepted event. The accepted parameter determines whether observationKind is TxAccepted or TxSubmit.</summary>
    public static CanFrameEvent CreateTransmitSubmission(
        ulong correlationId,
        CanFrame frame,
        bool accepted,
        ulong sequence = 0,
        int channelIndex = 0,
        DateTimeOffset? systemTimestampUtc = null,
        long deviceTimestampTicks = 0,
        long deviceTimestampFrequency = 0,
        CanTimestampKind deviceTimestampKind = default,
        uint nativeStatusCode = 0,
        uint nativeErrorCode = 0) =>
        new(sequence, frame,
            CanFrameDirection.Transmit,
            accepted ? CanFrameObservationKind.TxAccepted : CanFrameObservationKind.TxSubmit,
            correlationId, channelIndex,
            systemTimestampUtc ?? DateTimeOffset.UtcNow,
            deviceTimestampTicks, deviceTimestampFrequency,
            deviceTimestampKind, CanFrameEventFlags.None,
            CanTransmitOutcome.None, 0,
            nativeStatusCode, nativeErrorCode);

    /// <summary>判断两个帧事件是否相等（值比较）。<br/>Determines whether two frame events are equal (value comparison).</summary>
    public bool Equals(CanFrameEvent other) =>
        Sequence == other.Sequence &&
        Frame == other.Frame &&
        Direction == other.Direction &&
        ObservationKind == other.ObservationKind &&
        CorrelationId == other.CorrelationId &&
        ChannelIndex == other.ChannelIndex &&
        SystemTimestampUtc == other.SystemTimestampUtc &&
        DeviceTimestampTicks == other.DeviceTimestampTicks &&
        DeviceTimestampFrequency == other.DeviceTimestampFrequency &&
        DeviceTimestampKind == other.DeviceTimestampKind &&
        EventFlags == other.EventFlags &&
        Outcome == other.Outcome &&
        AttemptCount == other.AttemptCount &&
        NativeStatusCode == other.NativeStatusCode &&
        NativeErrorCode == other.NativeErrorCode;

    /// <inheritdoc cref="Equals(CanFrameEvent)"/>
    public override bool Equals(object? obj) => obj is CanFrameEvent other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Sequence);
        hash.Add(Frame);
        hash.Add(Direction);
        hash.Add(ObservationKind);
        hash.Add(CorrelationId);
        hash.Add(ChannelIndex);
        hash.Add(SystemTimestampUtc);
        hash.Add(DeviceTimestampTicks);
        hash.Add(DeviceTimestampFrequency);
        hash.Add(DeviceTimestampKind);
        hash.Add(EventFlags);
        hash.Add(Outcome);
        hash.Add(AttemptCount);
        hash.Add(NativeStatusCode);
        hash.Add(NativeErrorCode);
        return hash.ToHashCode();
    }

    /// <summary>判断两个帧事件是否相等。<br/>Determines whether two frame events are equal.</summary>
    public static bool operator ==(CanFrameEvent left, CanFrameEvent right) => left.Equals(right);

    /// <summary>判断两个帧事件是否不等。<br/>Determines whether two frame events are not equal.</summary>
    public static bool operator !=(CanFrameEvent left, CanFrameEvent right) => !left.Equals(right);

    /// <summary>返回帧事件的字符串表示。<br/>Returns a string representation of the frame event.</summary>
    public override string ToString()
    {
        var dir = Direction == CanFrameDirection.Transmit ? "TX" : "RX";
        var obs = ObservationKind.ToString();
        return $"CanFrameEvent[{dir}/{obs}] Seq={Sequence} Ch={ChannelIndex} {Frame}";
    }

    private static void ValidateChannelIndex(int channelIndex)
    {
        if (channelIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(channelIndex), channelIndex,
                "Channel index must be a non-negative application-assigned logical index.");
    }
}
