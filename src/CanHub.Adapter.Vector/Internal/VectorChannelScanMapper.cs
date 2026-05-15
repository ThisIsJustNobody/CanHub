using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 通道扫描映射器。将 XL Driver 原生通道配置转换为 CanHub 的 CanChannelInfo。<br/>
/// Vector channel scan mapper. Converts XL Driver native channel config to CanHub's CanChannelInfo.
/// </summary>
internal static class VectorChannelScanMapper
{
    /// <summary>
    /// 将 xl_channel_config 转换为 CanChannelInfo，包括设备名、能力检测和可用性判断。<br/>
    /// Converts xl_channel_config to CanChannelInfo, including device name, capability detection,
    /// and availability assessment.
    /// </summary>
    public static CanChannelInfo FromChannelConfig(XLClass.xl_channel_config channel)
    {
        var deviceName = VectorDeviceTypeMapper.ToEndpointDeviceName(channel.hwType);
        var deviceIndex = channel.hwIndex;
        var logicalChannelIndex = channel.hwChannel;
        var nativeChannelIndex = channel.channelIndex;
        var isCanCompatible = HasBusCapability(channel.channelBusCapabilities, XLDefine.XL_BusCapabilities.XL_BUS_COMPATIBLE_CAN)
            || HasBusCapability(channel.channelBusCapabilities, XLDefine.XL_BusCapabilities.XL_BUS_ACTIVE_CAP_CAN);

        if (!isCanCompatible)
        {
            return new CanChannelInfo(
                adapterId: "vector",
                deviceName: deviceName,
                deviceIndex: deviceIndex,
                channelIndex: logicalChannelIndex,
                nativeChannelIndex: nativeChannelIndex,
                endpoint: null,
                availability: CanChannelAvailability.Unsupported,
                diagnostic: new ScanDiagnostic(
                    CanErrorCategory.InvalidEndpoint,
                    "Vector channel is not CAN-compatible.",
                    adapterId: "vector"));
        }

        var capabilities = new List<CanCapability>
        {
            new("classic-can", false, "Classic CAN support"),
        };

        var supportsIsoFd = HasChannelCapability(channel.channelCapabilities, XLDefine.XL_ChannelCapabilities.XL_CHANNEL_FLAG_CANFD_ISO_SUPPORT);
        var supportsBoschFd = HasChannelCapability(channel.channelCapabilities, XLDefine.XL_ChannelCapabilities.XL_CHANNEL_FLAG_CANFD_BOSCH_SUPPORT);

        if (supportsIsoFd || supportsBoschFd)
        {
            capabilities.Add(new CanCapability("can-fd", false, "CAN FD support"));
        }

        if (supportsIsoFd)
        {
            capabilities.Add(new CanCapability("iso-can-fd", false, "ISO CAN FD support"));
        }

        if (supportsBoschFd)
        {
            capabilities.Add(new CanCapability("non-iso-fd", false, "BOSCH CAN FD support"));
        }

        var endpoint = $"vector://{deviceName}?deviceIndex={deviceIndex}&channel={logicalChannelIndex}";
        var availability = channel.isOnBus == 0
            ? CanChannelAvailability.Available
            : CanChannelAvailability.Active;

        return new CanChannelInfo(
            adapterId: "vector",
            deviceName: deviceName,
            deviceIndex: deviceIndex,
            channelIndex: logicalChannelIndex,
            nativeChannelIndex: nativeChannelIndex,
            endpoint: endpoint,
            availability: availability,
            capabilities: capabilities);
    }

    private static bool HasBusCapability(
        XLDefine.XL_BusCapabilities value,
        XLDefine.XL_BusCapabilities flag)
        => (value & flag) == flag;

    private static bool HasChannelCapability(
        XLDefine.XL_ChannelCapabilities value,
        XLDefine.XL_ChannelCapabilities flag)
        => (value & flag) == flag;
}
