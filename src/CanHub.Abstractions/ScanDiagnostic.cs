namespace CanHub;

/// <summary>
/// CAN 通道扫描失败的结构化诊断。<br/>
/// Structured diagnostic for a CAN channel scan failure.
/// </summary>
public sealed class ScanDiagnostic
{
    /// <summary>适配器唯一标识符。<br/>Unique adapter identifier.</summary>
    public string AdapterId { get; }

    /// <summary>诊断分类。<br/>Diagnostic category.</summary>
    public CanErrorCategory Category { get; }

    /// <summary>人类可读的诊断消息。<br/>Human-readable diagnostic message.</summary>
    public string Message { get; }

    /// <summary>原生错误码（如有）。<br/>Native error code (if any).</summary>
    public int? NativeErrorCode { get; }

    /// <summary>错误的可恢复级别。<br/>Error recoverability level.</summary>
    public CanRecoverability Recoverability { get; }

    /// <summary>关联的端点（如有）。<br/>Associated endpoint (if any).</summary>
    public string? Endpoint { get; }

    /// <summary>面向排障的人类可读提示（如有）。<br/>Human-readable troubleshooting hint (if any).</summary>
    public string? Hint { get; }

    /// <summary>结构化诊断详情。<br/>Structured diagnostic details.</summary>
    public IReadOnlyDictionary<string, string> Details { get; }

    /// <summary>创建扫描诊断记录。<br/>Creates a scan diagnostic record.</summary>
    public ScanDiagnostic(
        CanErrorCategory category,
        string message,
        int? nativeErrorCode = null,
        CanRecoverability recoverability = CanRecoverability.Fatal,
        string adapterId = "*",
        string? endpoint = null,
        string? hint = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        AdapterId = adapterId;
        Category = category;
        Message = message;
        NativeErrorCode = nativeErrorCode;
        Recoverability = recoverability;
        Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint;
        Hint = string.IsNullOrWhiteSpace(hint) ? null : hint;
        Details = details is null || details.Count == 0
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(details, StringComparer.Ordinal);
    }

    /// <summary>
    /// 创建扫描诊断记录，保留旧版二进制签名。<br/>
    /// Creates a scan diagnostic record, preserving the legacy binary signature.
    /// </summary>
    public ScanDiagnostic(
        CanErrorCategory category,
        string message,
        int? nativeErrorCode,
        CanRecoverability recoverability,
        string adapterId,
        string? endpoint)
        : this(category, message, nativeErrorCode, recoverability, adapterId, endpoint, null, null)
    {
    }
}
