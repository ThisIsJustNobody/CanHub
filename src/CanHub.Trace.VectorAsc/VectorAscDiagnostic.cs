namespace CanHub.Trace.VectorAsc;

/// <summary>
/// ASC 诊断代码。<br/>
/// Diagnostic codes emitted by ASC parsing.
/// </summary>
public static class VectorAscDiagnosticCodes
{
    /// <summary>无法解析日期。<br/>A date value could not be parsed.</summary>
    public const string UnparseableDate = "ASC001";

    /// <summary>不支持或已跳过的行。<br/>An unsupported row was skipped.</summary>
    public const string UnsupportedLine = "ASC002";

    /// <summary>行格式无效。<br/>A row is malformed.</summary>
    public const string MalformedLine = "ASC003";

    /// <summary>CAN FD flags 与显式 BRS/ESI 字段不一致。<br/>CAN FD flags disagree with explicit BRS/ESI fields.</summary>
    public const string CanFdFlagsMismatch = "ASC004";

    /// <summary>CAN FD DLC 与数据长度不一致。<br/>CAN FD DLC and data length disagree.</summary>
    public const string CanFdDlcLengthMismatch = "ASC005";
}

/// <summary>
/// ASC 读取诊断。<br/>
/// Diagnostic emitted while reading an ASC file.
/// </summary>
/// <param name="LineNumber">1 基行号。<br/>One-based line number.</param>
/// <param name="Code">诊断代码。<br/>Diagnostic code.</param>
/// <param name="Message">诊断消息。<br/>Diagnostic message.</param>
/// <param name="Severity">严重级别。<br/>Severity.</param>
public sealed record VectorAscDiagnostic(
    int LineNumber,
    string Code,
    string Message,
    VectorAscDiagnosticSeverity Severity = VectorAscDiagnosticSeverity.Warning);
