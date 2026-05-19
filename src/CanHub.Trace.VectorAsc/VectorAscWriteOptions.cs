namespace CanHub.Trace.VectorAsc;

/// <summary>
/// ASC 写入选项。<br/>
/// Options used while writing ASC files.
/// </summary>
public sealed class VectorAscWriteOptions
{
    /// <summary>
    /// Trace 起始时间。为空时使用当前本地时间。<br/>
    /// Trace start time. Uses the current local time when null.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// 输出数值进制。当前默认写出十六进制。<br/>
    /// Numeric base for output. Hexadecimal is the default.
    /// </summary>
    public VectorAscNumericBase NumericBase { get; init; } = VectorAscNumericBase.Hex;

    /// <summary>
    /// 输出时间戳格式。当前默认写出 absolute。<br/>
    /// Timestamp format for output. Absolute is the default.
    /// </summary>
    public VectorAscTimestampFormat TimestampFormat { get; init; } = VectorAscTimestampFormat.Absolute;
}
