namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 设备能力描述记录。包含端点名称、设备类型 ID 和功能支持信息。<br/>
/// ZLG device capabilities record. Contains endpoint name, device type ID, and feature support information.
/// </summary>
/// <param name="EndpointName">端点名称（如 USBCANFD_200U）。<br/>Endpoint name (e.g. USBCANFD_200U).</param>
/// <param name="DeviceTypeId">设备类型 ID。<br/>Device type ID.</param>
/// <param name="SupportsCanFd">是否支持 CAN FD。<br/>Whether CAN FD is supported.</param>
/// <param name="SupportsMergedReceive">是否支持合并接收模式。<br/>Whether merged receive mode is supported.</param>
/// <param name="DefaultChannelCount">默认通道数。<br/>Default channel count.</param>
internal sealed record ZlgDeviceCapabilities(
    string EndpointName,
    uint DeviceTypeId,
    bool SupportsCanFd,
    bool SupportsMergedReceive,
    int DefaultChannelCount);

/// <summary>
/// ZLG 设备类型映射表。根据端点名称解析设备能力，并提供可扫描设备类型列表。<br/>
/// ZLG device type map. Resolves device capabilities by endpoint name and provides a list of scannable device types.
/// </summary>
internal static class ZlgDeviceTypeMap
{
    /// <summary>
    /// USBCANFD_200U 端点名称常量。<br/>
    /// USBCANFD_200U endpoint name constant.
    /// </summary>
    public const string UsbCanFd200UEndpointName = "USBCANFD_200U";

    private static readonly ZlgDeviceCapabilities s_usbCanFd200U = new(
        UsbCanFd200UEndpointName,
        (uint)ZlgDeviceType.UsbCanFd200U,
        SupportsCanFd: true,
        SupportsMergedReceive: true,
        DefaultChannelCount: 2);

    /// <summary>
    /// 根据设备名称解析设备能力。v1 仅支持 USBCANFD_200U。<br/>
    /// Resolves device capabilities by device name. v1 supports only USBCANFD_200U.
    /// </summary>
    public static ZlgDeviceCapabilities Resolve(string device)
    {
        var normalized = Normalize(device);
        if (normalized is "USBCANFD_200U" or "ZCAN_USBCANFD_200U")
            return s_usbCanFd200U;

        throw new CanException("zlg", CanErrorCategory.InvalidEndpoint,
            $"Unsupported ZLG device '{device}'. v1 supports {UsbCanFd200UEndpointName}.");
    }

    /// <summary>
    /// 获取所有可扫描的设备类型列表。<br/>
    /// Gets the list of all scannable device types.
    /// </summary>
    public static IEnumerable<ZlgDeviceCapabilities> GetScannableDeviceTypes()
    {
        yield return s_usbCanFd200U;
    }

    private static string Normalize(string value) =>
        value.Trim()
            .Replace('-', '_')
            .ToUpperInvariant();
}
