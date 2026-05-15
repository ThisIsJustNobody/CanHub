namespace CanHub;

/// <summary>
/// 可发现的 CAN 通道扫描结果。<br/>
/// A discoverable CAN channel scan result.
/// </summary>
public sealed class CanChannelInfo
{
    /// <summary>适配器唯一标识符。<br/>Unique adapter identifier.</summary>
    public string AdapterId { get; }

    /// <summary>设备显示名称。<br/>Device display name.</summary>
    public string DeviceName { get; }

    /// <summary>设备索引（逻辑编号）。<br/>Device index (logical number).</summary>
    public int DeviceIndex { get; }

    /// <summary>通道索引（逻辑编号）。<br/>Channel index (logical number).</summary>
    public int ChannelIndex { get; }

    /// <summary>原生驱动通道索引（如有）。<br/>Native driver channel index (if any).</summary>
    public int? NativeChannelIndex { get; }

    /// <summary>用于打开此通道的端点 URI（如有）。<br/>Endpoint URI for opening this channel (if any).</summary>
    public string? Endpoint { get; }

    /// <summary>通道可用性状态。<br/>Channel availability status.</summary>
    public CanChannelAvailability Availability { get; }

    /// <summary>是否可以打开此通道。<br/>Whether this channel can be opened.</summary>
    public bool CanOpen { get; }

    /// <summary>此通道支持的适配器能力列表。<br/>List of adapter capabilities supported by this channel.</summary>
    public IReadOnlyList<CanCapability> Capabilities { get; }

    /// <summary>通道扫描的详细诊断（如有）。<br/>Detailed scan diagnostic (if any).</summary>
    public ScanDiagnostic? Diagnostic { get; }

    /// <summary>创建一个 CAN 通道扫描条目。<br/>Creates a CAN channel scan entry.</summary>
    public CanChannelInfo(
        string adapterId,
        string deviceName,
        int deviceIndex,
        int channelIndex,
        int? nativeChannelIndex,
        string? endpoint,
        CanChannelAvailability availability,
        IReadOnlyList<CanCapability>? capabilities = null,
        ScanDiagnostic? diagnostic = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentOutOfRangeException.ThrowIfNegative(deviceIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(channelIndex);
        if (nativeChannelIndex is < 0)
            throw new ArgumentOutOfRangeException(nameof(nativeChannelIndex), nativeChannelIndex, "Native channel index must be non-negative.");

        AdapterId = adapterId;
        DeviceName = deviceName;
        DeviceIndex = deviceIndex;
        ChannelIndex = channelIndex;
        NativeChannelIndex = nativeChannelIndex;
        Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint;
        Availability = availability;
        Capabilities = capabilities is not null
            ? Array.AsReadOnly(capabilities.ToArray())
            : Array.AsReadOnly(Array.Empty<CanCapability>());
        Diagnostic = diagnostic;
        CanOpen = Endpoint is not null
            && availability is not CanChannelAvailability.Unsupported
            && availability is not CanChannelAvailability.Error;
    }
}
