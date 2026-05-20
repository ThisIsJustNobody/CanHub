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

    /// <summary>面向用户界面的通道显示名称。<br/>User-facing channel display name.</summary>
    public string DisplayName { get; }

    /// <summary>稳定通道标识。优先使用规范端点。<br/>Stable channel identifier. Prefers the canonical endpoint.</summary>
    public string ChannelId { get; }

    /// <summary>厂商名称（如有）。<br/>Vendor name (if any).</summary>
    public string? VendorName { get; }

    /// <summary>硬件标识（如有）。<br/>Hardware identifier (if any).</summary>
    public string? HardwareId { get; }

    /// <summary>设备序列号（如有）。<br/>Device serial number (if any).</summary>
    public string? SerialNumber { get; }

    /// <summary>设备索引（逻辑编号）。<br/>Device index (logical number).</summary>
    public int DeviceIndex { get; }

    /// <summary>通道索引（逻辑编号）。<br/>Channel index (logical number).</summary>
    public int ChannelIndex { get; }

    /// <summary>原生驱动通道索引（如有）。<br/>Native driver channel index (if any).</summary>
    public int? NativeChannelIndex { get; }

    /// <summary>用于打开此通道的端点 URI（如有）。<br/>Endpoint URI for opening this channel (if any).</summary>
    public string? Endpoint { get; }

    /// <summary>规范化端点 URI（如有）。<br/>Canonical endpoint URI (if any).</summary>
    public string? CanonicalEndpoint { get; }

    /// <summary>推荐的默认总线参数（如有）。<br/>Recommended default bus parameters (if any).</summary>
    public CanBusParameters? RecommendedBusParameters { get; }

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
        ScanDiagnostic? diagnostic = null,
        string? channelId = null,
        string? displayName = null,
        string? vendorName = null,
        string? hardwareId = null,
        string? serialNumber = null,
        string? canonicalEndpoint = null,
        CanBusParameters? recommendedBusParameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentOutOfRangeException.ThrowIfNegative(deviceIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(channelIndex);
        if (nativeChannelIndex is < 0)
            throw new ArgumentOutOfRangeException(nameof(nativeChannelIndex), nativeChannelIndex, "Native channel index must be non-negative.");

        AdapterId = adapterId;
        DeviceName = deviceName;
        VendorName = string.IsNullOrWhiteSpace(vendorName) ? null : vendorName;
        HardwareId = string.IsNullOrWhiteSpace(hardwareId) ? null : hardwareId;
        SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber;
        DeviceIndex = deviceIndex;
        ChannelIndex = channelIndex;
        NativeChannelIndex = nativeChannelIndex;
        Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint;
        CanonicalEndpoint = string.IsNullOrWhiteSpace(canonicalEndpoint)
            ? TryCanonicalizeEndpoint(Endpoint)
            : canonicalEndpoint;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? $"{deviceName} Channel {channelIndex}"
            : displayName;
        ChannelId = string.IsNullOrWhiteSpace(channelId)
            ? CanonicalEndpoint ?? $"{adapterId}:{deviceName}:{deviceIndex}:{channelIndex}"
            : channelId;
        RecommendedBusParameters = recommendedBusParameters;
        Availability = availability;
        Capabilities = capabilities is not null
            ? Array.AsReadOnly(capabilities.ToArray())
            : Array.AsReadOnly(Array.Empty<CanCapability>());
        Diagnostic = diagnostic;
        CanOpen = Endpoint is not null
            && availability is not CanChannelAvailability.Unsupported
            && availability is not CanChannelAvailability.Error;
    }

    /// <summary>
    /// 创建一个 CAN 通道扫描条目，保留旧版二进制签名。<br/>
    /// Creates a CAN channel scan entry, preserving the legacy binary signature.
    /// </summary>
    public CanChannelInfo(
        string adapterId,
        string deviceName,
        int deviceIndex,
        int channelIndex,
        int? nativeChannelIndex,
        string? endpoint,
        CanChannelAvailability availability,
        IReadOnlyList<CanCapability>? capabilities,
        ScanDiagnostic? diagnostic)
        : this(
            adapterId,
            deviceName,
            deviceIndex,
            channelIndex,
            nativeChannelIndex,
            endpoint,
            availability,
            capabilities,
            diagnostic,
            channelId: null,
            displayName: null,
            vendorName: null,
            hardwareId: null,
            serialNumber: null,
            canonicalEndpoint: null,
            recommendedBusParameters: null)
    {
    }

    private static string? TryCanonicalizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        try
        {
            return CanEndpoint.Parse(endpoint).ToString();
        }
        catch (CanException)
        {
            return endpoint;
        }
    }
}
