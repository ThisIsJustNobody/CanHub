namespace CanHub.Trace.VectorAsc;

/// <summary>
/// ASC 数值进制。<br/>
/// Numeric base used by an ASC file.
/// </summary>
public enum VectorAscNumericBase
{
    /// <summary>十六进制。<br/>Hexadecimal.</summary>
    Hex = 16,

    /// <summary>十进制。<br/>Decimal.</summary>
    Decimal = 10
}

/// <summary>
/// ASC 时间戳格式。<br/>
/// Timestamp mode declared by an ASC file.
/// </summary>
public enum VectorAscTimestampFormat
{
    /// <summary>绝对时间戳，表示相对触发块起点的偏移秒。<br/>Absolute timestamp offsets from trigger block start.</summary>
    Absolute = 0,

    /// <summary>相对时间戳，表示与上一条事件的间隔秒。<br/>Relative timestamp deltas from the previous event.</summary>
    Relative = 1
}

/// <summary>
/// ASC 诊断严重级别。<br/>
/// Severity for ASC diagnostics.
/// </summary>
public enum VectorAscDiagnosticSeverity
{
    /// <summary>信息。<br/>Informational.</summary>
    Information = 0,

    /// <summary>警告。<br/>Warning.</summary>
    Warning = 1,

    /// <summary>错误。<br/>Error.</summary>
    Error = 2
}
