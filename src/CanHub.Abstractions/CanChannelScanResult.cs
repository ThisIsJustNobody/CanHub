namespace CanHub;

/// <summary>
/// CAN 通道扫描结果，包含可列出的通道和适配器级诊断。<br/>
/// CAN channel scan result, containing enumerable channels and adapter-level diagnostics.
/// </summary>
public sealed class CanChannelScanResult
{
    /// <summary>可发现的 CAN 通道列表。<br/>List of discoverable CAN channels.</summary>
    public IReadOnlyList<CanChannelInfo> Channels { get; }

    /// <summary>适配器级扫描诊断列表。<br/>List of adapter-level scan diagnostics.</summary>
    public IReadOnlyList<ScanDiagnostic> Diagnostics { get; }

    /// <summary>创建 CAN 通道扫描结果。<br/>Creates a CAN channel scan result.</summary>
    public CanChannelScanResult(
        IReadOnlyList<CanChannelInfo>? channels = null,
        IReadOnlyList<ScanDiagnostic>? diagnostics = null)
    {
        Channels = channels ?? Array.Empty<CanChannelInfo>();
        Diagnostics = diagnostics ?? Array.Empty<ScanDiagnostic>();
    }
}
