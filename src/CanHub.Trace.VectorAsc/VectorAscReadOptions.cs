namespace CanHub.Trace.VectorAsc;

/// <summary>
/// ASC 读取选项。<br/>
/// Options used while reading ASC files.
/// </summary>
public sealed class VectorAscReadOptions
{
    /// <summary>
    /// 是否启用严格模式。严格模式会在遇到格式错误的受支持行时抛出异常。<br/>
    /// Whether strict mode is enabled. Strict mode throws for malformed supported rows.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// 诊断回调。用于流式读取时接收诊断而不累计完整诊断列表。<br/>
    /// Diagnostic callback. Useful for streaming reads without accumulating a full diagnostics list.
    /// </summary>
    public Action<VectorAscDiagnostic>? DiagnosticSink { get; init; }
}
