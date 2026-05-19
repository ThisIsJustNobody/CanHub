namespace CanHub;

/// <summary>
/// CanHub 低频状态事件的大类别。<br/>
/// Broad category for low-frequency CanHub status events.
/// </summary>
public enum CanStatusKind : byte
{
    /// <summary>未指定状态类别。<br/>No status kind specified.</summary>
    None = 0,

    /// <summary>设备级状态。<br/>Device-level status.</summary>
    Device = 1,

    /// <summary>通道级状态。<br/>Channel-level status.</summary>
    Channel = 2,

    /// <summary>驱动/运行时状态。<br/>Driver/runtime status.</summary>
    Driver = 3,

    /// <summary>订阅队列状态。<br/>Subscription queue status.</summary>
    Subscription = 4,

    /// <summary>CAN 总线/控制器状态。<br/>CAN bus/controller status.</summary>
    Bus = 5,

    /// <summary>发送路径状态。<br/>Transmit path status.</summary>
    Transmit = 6,

    /// <summary>接收路径状态。<br/>Receive path status.</summary>
    Receive = 7
}

/// <summary>
/// CanHub 低频状态事件的公开状态代码。<br/>
/// Public status code for low-frequency CanHub status events.
/// </summary>
public enum CanStatusCode : ushort
{
    /// <summary>未指定状态代码。<br/>No status code specified.</summary>
    None = 0,

    /// <summary>资源已启动。<br/>Resource started.</summary>
    Started = 1,

    /// <summary>资源已停止。<br/>Resource stopped.</summary>
    Stopped = 2,

    /// <summary>设备或通道已连接。<br/>Device or channel connected.</summary>
    Connected = 3,

    /// <summary>设备或通道已断开。<br/>Device or channel disconnected.</summary>
    Disconnected = 4,

    /// <summary>CAN 控制器进入总线关闭状态。<br/>CAN controller entered bus-off state.</summary>
    BusOff = 100,

    /// <summary>CAN 控制器从总线关闭或等效降级状态恢复。<br/>CAN controller recovered from bus-off or an equivalent degraded state.</summary>
    BusRecovered = 101,

    /// <summary>队列压力超过报告阈值。<br/>Queue pressure crossed a reporting threshold.</summary>
    QueuePressure = 200,

    /// <summary>队列已满。<br/>Queue is full.</summary>
    QueueFull = 201,

    /// <summary>一个或多个帧被丢弃。<br/>One or more frames were dropped.</summary>
    DroppedFrames = 202,

    /// <summary>原生驱动报告了一个错误。<br/>Native driver reported an error.</summary>
    NativeDriverError = 300,

    /// <summary>原生驱动报告了一个非错误事件。<br/>Native driver reported a non-error event.</summary>
    NativeDriverEvent = 301,

    /// <summary>自动恢复正在启动。<br/>Automatic recovery is starting.</summary>
    Recovering = 400,

    /// <summary>自动恢复已完成。<br/>Automatic recovery completed.</summary>
    Recovered = 401,

    /// <summary>自动恢复失败。<br/>Automatic recovery failed.</summary>
    RecoveryFailed = 402,

    /// <summary>自动恢复被跳过。<br/>Automatic recovery was skipped.</summary>
    RecoverySkipped = 403,

    /// <summary>配置参数被忽略，因为适配器不支持。<br/>Configuration parameter was ignored because the adapter does not support it.</summary>
    ConfigurationIgnored = 500,
}

/// <summary>
/// CanHub 低频状态事件的严重性级别。<br/>
/// Severity for low-frequency CanHub status events.
/// </summary>
public enum CanStatusSeverity : byte
{
    /// <summary>未指定严重性。<br/>No severity specified.</summary>
    None = 0,

    /// <summary>信息性状态。<br/>Informational status.</summary>
    Info = 1,

    /// <summary>警告状态。<br/>Warning status.</summary>
    Warning = 2,

    /// <summary>错误状态。<br/>Error status.</summary>
    Error = 3,

    /// <summary>严重状态，可能需要用户关注。<br/>Critical status that likely needs user attention.</summary>
    Critical = 4
}

/// <summary>
/// 低频状态事件，用于设备、通道、驱动、总线和订阅队列状态。有意与 <see cref="CanFrameEvent"/> 分离。<br/>
/// Low-frequency status event for device, channel, driver, bus, and subscription queue state.
/// This is intentionally separate from <see cref="CanFrameEvent"/>.
/// </summary>
public readonly struct CanStatusEvent : IEquatable<CanStatusEvent>
{
    /// <summary>序列号，用于与其他状态/帧观测排序。<br/>Sequence number for ordering with other status/frame observations when available.</summary>
    public ulong Sequence { get; }

    /// <summary>状态类别。<br/>Status category.</summary>
    public CanStatusKind Kind { get; }

    /// <summary>公开状态码。<br/>Public status code.</summary>
    public CanStatusCode Code { get; }

    /// <summary>状态严重性。<br/>Status severity.</summary>
    public CanStatusSeverity Severity { get; }

    /// <summary>应用程序分配的逻辑通道索引。这不是供应商驱动通道号。<br/>Application-assigned logical channel index. This is not a vendor driver channel number.</summary>
    public int ChannelIndex { get; }

    /// <summary>系统时间戳（UTC）。<br/>System timestamp (UTC).</summary>
    public DateTimeOffset SystemTimestampUtc { get; }

    /// <summary>关联的帧序列号。未关联帧时为 0。<br/>Related frame sequence. 0 when not associated with a frame.</summary>
    public ulong RelatedFrameSequence { get; }

    /// <summary>关联的发送关联 ID。未关联发送请求时为 0。<br/>Related transmit correlation id. 0 when not associated with a transmit request.</summary>
    public ulong CorrelationId { get; }

    /// <summary>可选事件计数，如丢帧计数。不适用时为 0。<br/>Optional event count, such as dropped frame count. 0 when not applicable.</summary>
    public ulong Count { get; }

    /// <summary>驱动返回的原始状态码。<br/>Native status code from the driver.</summary>
    public uint NativeStatusCode { get; }

    /// <summary>驱动返回的原始错误码。<br/>Native error code from the driver.</summary>
    public uint NativeErrorCode { get; }

    /// <summary>可选的诊断消息，提供人类可读的上下文。<br/>Optional diagnostic message providing human-readable context.</summary>
    public string? Message { get; }

    /// <summary>此值是否作为具体状态事件创建。<br/>Whether this value was created as a concrete status event.</summary>
    public bool IsInitialized =>
        Kind != CanStatusKind.None || Code != CanStatusCode.None || Severity != CanStatusSeverity.None;

    private CanStatusEvent(
        ulong sequence,
        CanStatusKind kind,
        CanStatusCode code,
        CanStatusSeverity severity,
        int channelIndex,
        DateTimeOffset systemTimestampUtc,
        ulong relatedFrameSequence,
        ulong correlationId,
        ulong count,
        uint nativeStatusCode,
        uint nativeErrorCode,
        string? message)
    {
        Sequence = sequence;
        Kind = kind;
        Code = code;
        Severity = severity;
        ChannelIndex = channelIndex;
        SystemTimestampUtc = systemTimestampUtc;
        RelatedFrameSequence = relatedFrameSequence;
        CorrelationId = correlationId;
        Count = count;
        NativeStatusCode = nativeStatusCode;
        NativeErrorCode = nativeErrorCode;
        Message = message;
    }

    /// <summary>创建低频 CanHub 状态事件。要求 kind、code、severity 均非 None，且 channelIndex 非负。<br/>Create a low-frequency CanHub status event. Requires kind, code, severity all be non-None, and channelIndex be non-negative.</summary>
    public static CanStatusEvent Create(
        CanStatusKind kind,
        CanStatusCode code,
        CanStatusSeverity severity,
        ulong sequence = 0,
        int channelIndex = 0,
        DateTimeOffset? systemTimestampUtc = null,
        ulong relatedFrameSequence = 0,
        ulong correlationId = 0,
        ulong count = 0,
        uint nativeStatusCode = 0,
        uint nativeErrorCode = 0,
        string? message = null)
    {
        if (kind == CanStatusKind.None)
            throw new ArgumentOutOfRangeException(nameof(kind), kind,
                "Status kind must not be None.");
        if (code == CanStatusCode.None)
            throw new ArgumentOutOfRangeException(nameof(code), code,
                "Status code must not be None.");
        if (severity == CanStatusSeverity.None)
            throw new ArgumentOutOfRangeException(nameof(severity), severity,
                "Status severity must not be None.");
        if (channelIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(channelIndex), channelIndex,
                "Channel index must be a non-negative application-assigned logical index.");

        return new(sequence, kind, code, severity, channelIndex,
            systemTimestampUtc ?? DateTimeOffset.UtcNow,
            relatedFrameSequence, correlationId, count,
            nativeStatusCode, nativeErrorCode, message);
    }

    /// <summary>判断两个状态事件是否相等。<br/>Determines whether two status events are equal.</summary>
    public bool Equals(CanStatusEvent other) =>
        Sequence == other.Sequence &&
        Kind == other.Kind &&
        Code == other.Code &&
        Severity == other.Severity &&
        ChannelIndex == other.ChannelIndex &&
        SystemTimestampUtc == other.SystemTimestampUtc &&
        RelatedFrameSequence == other.RelatedFrameSequence &&
        CorrelationId == other.CorrelationId &&
        Count == other.Count &&
        NativeStatusCode == other.NativeStatusCode &&
        NativeErrorCode == other.NativeErrorCode &&
        Message == other.Message;

    /// <inheritdoc cref="Equals(CanStatusEvent)"/>
    public override bool Equals(object? obj) => obj is CanStatusEvent other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Sequence);
        hash.Add(Kind);
        hash.Add(Code);
        hash.Add(Severity);
        hash.Add(ChannelIndex);
        hash.Add(SystemTimestampUtc);
        hash.Add(RelatedFrameSequence);
        hash.Add(CorrelationId);
        hash.Add(Count);
        hash.Add(NativeStatusCode);
        hash.Add(NativeErrorCode);
        hash.Add(Message);
        return hash.ToHashCode();
    }

    /// <summary>
    /// 创建 <see cref="CanStatusCode.ConfigurationIgnored"/> 事件的工厂方法。
    /// 当适配器无法支持请求的总线参数时，创建一个 Warning 级别的 Channel 事件。<br/>
    /// Factory for <see cref="CanStatusCode.ConfigurationIgnored"/> events.
    /// Creates a Warning-level Channel event when an adapter cannot support
    /// a requested bus parameter.
    /// </summary>
    public static CanStatusEvent ConfigurationIgnored(
        string paramName, string reason, int channelIndex = 0)
        => Create(
            CanStatusKind.Channel,
            CanStatusCode.ConfigurationIgnored,
            CanStatusSeverity.Warning,
            channelIndex: channelIndex,
            message: $"参数 '{paramName}' 被忽略: {reason}");

    /// <summary>判断两个状态事件是否相等。<br/>Determines whether two status events are equal.</summary>
    public static bool operator ==(CanStatusEvent left, CanStatusEvent right) => left.Equals(right);

    /// <summary>判断两个状态事件是否不等。<br/>Determines whether two status events are not equal.</summary>
    public static bool operator !=(CanStatusEvent left, CanStatusEvent right) => !left.Equals(right);

    /// <summary>返回状态事件的字符串表示。<br/>Returns a string representation of the status event.</summary>
    public override string ToString()
    {
        if (!IsInitialized) return "CanStatusEvent[uninitialized]";
        return $"CanStatusEvent[{Kind}/{Code}] Sev={Severity} Ch={ChannelIndex}{(Message is { } m ? $" \"{m}\"" : "")}";
    }
}
