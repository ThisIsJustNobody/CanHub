namespace CanHub;

/// <summary>
/// 适配器机器可读描述。<br/>
/// Machine-readable adapter descriptor.
/// </summary>
public sealed class CanAdapterManifest
{
    /// <summary>适配器唯一标识符。<br/>Unique adapter identifier.</summary>
    public string AdapterId { get; }

    /// <summary>适配器显示名称。<br/>Adapter display name.</summary>
    public string DisplayName { get; }

    /// <summary>支持的端点 URI 方案列表（如 "usb"、"pcie"、"tcp"）。<br/>List of supported endpoint URI schemes (e.g., "usb", "pcie", "tcp").</summary>
    public IReadOnlyList<string> EndpointSchemes { get; }

    /// <summary>目标平台（如 "windows"、"linux"、"cross-platform"）。<br/>Target platform (e.g., "windows", "linux", "cross-platform").</summary>
    public string Platform { get; }

    /// <summary>设备独占模型。<br/>Device exclusivity model.</summary>
    public ExclusivityModel Exclusivity { get; }

    /// <summary>适配器支持的能力列表（如 CAN FD、J1939）。<br/>List of capabilities the adapter supports (e.g., CAN FD, J1939).</summary>
    public IReadOnlyList<CanCapability> Capabilities { get; }

    /// <summary>是否支持自动 CAN 通道扫描。<br/>Whether automatic CAN channel scan is supported.</summary>
    public bool SupportsChannelScan { get; }

    /// <summary>创建适配器清单。<br/>Creates an adapter manifest.</summary>
    public CanAdapterManifest(
        string adapterId,
        string displayName,
        IReadOnlyList<string> endpointSchemes,
        string platform = "cross-platform",
        ExclusivityModel exclusivity = ExclusivityModel.None,
        IReadOnlyList<CanCapability>? capabilities = null,
        bool supportsChannelScan = false)
    {
        AdapterId = adapterId ?? throw new ArgumentNullException(nameof(adapterId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        EndpointSchemes = Array.AsReadOnly((endpointSchemes ?? throw new ArgumentNullException(nameof(endpointSchemes))).ToArray());
        Platform = platform ?? throw new ArgumentNullException(nameof(platform));
        Exclusivity = exclusivity;
        Capabilities = capabilities is not null
            ? Array.AsReadOnly(capabilities.ToArray())
            : Array.AsReadOnly(Array.Empty<CanCapability>());
        SupportsChannelScan = supportsChannelScan;
    }
}
