namespace CanHub.Trace.VectorAsc;

/// <summary>
/// 已解析的 ASC 文件。<br/>
/// Parsed ASC file.
/// </summary>
public sealed class VectorAscFile
{
    /// <summary>
    /// 初始化 ASC 文件结果。<br/>
    /// Initializes an ASC file result.
    /// </summary>
    public VectorAscFile(
        VectorAscNumericBase numericBase,
        VectorAscTimestampFormat timestampFormat,
        bool internalEventsLogged,
        string? rawDateText,
        DateTimeOffset? fileDate,
        string? rawTriggerBlockText,
        DateTimeOffset? triggerBlockStart,
        IReadOnlyList<VectorAscFrame> frames,
        IReadOnlyList<VectorAscDiagnostic> diagnostics)
    {
        NumericBase = numericBase;
        TimestampFormat = timestampFormat;
        InternalEventsLogged = internalEventsLogged;
        RawDateText = rawDateText;
        FileDate = fileDate;
        RawTriggerBlockText = rawTriggerBlockText;
        TriggerBlockStart = triggerBlockStart;
        Frames = frames;
        Diagnostics = diagnostics;
    }

    /// <summary>ASC 数值进制。<br/>ASC numeric base.</summary>
    public VectorAscNumericBase NumericBase { get; }

    /// <summary>ASC 时间戳格式。<br/>ASC timestamp format.</summary>
    public VectorAscTimestampFormat TimestampFormat { get; }

    /// <summary>文件是否声明记录内部事件。<br/>Whether the file declares internal events logging.</summary>
    public bool InternalEventsLogged { get; }

    /// <summary>原始 date 文本。<br/>Raw date text.</summary>
    public string? RawDateText { get; }

    /// <summary>解析后的文件日期。<br/>Parsed file date.</summary>
    public DateTimeOffset? FileDate { get; }

    /// <summary>原始触发块时间文本。<br/>Raw trigger block date text.</summary>
    public string? RawTriggerBlockText { get; }

    /// <summary>解析后的触发块开始时间。<br/>Parsed trigger block start time.</summary>
    public DateTimeOffset? TriggerBlockStart { get; }

    /// <summary>已解析帧。<br/>Parsed frames.</summary>
    public IReadOnlyList<VectorAscFrame> Frames { get; }

    /// <summary>读取诊断。<br/>Read diagnostics.</summary>
    public IReadOnlyList<VectorAscDiagnostic> Diagnostics { get; }
}
