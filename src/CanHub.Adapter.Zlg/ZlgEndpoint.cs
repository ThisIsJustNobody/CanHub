using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg;

/// <summary>
/// ZLG 端点构造辅助工具。用于固定设备配置场景，扫描结果仍优先使用 <see cref="CanChannelInfo.CanonicalEndpoint"/>。<br/>
/// ZLG endpoint builder. Use for fixed device configurations; scanned channels should still prefer <see cref="CanChannelInfo.CanonicalEndpoint"/>.
/// </summary>
public static class ZlgEndpoint
{
    /// <summary>
    /// 创建 ZLG 通道端点。<br/>
    /// Creates a ZLG channel endpoint.
    /// </summary>
    public static CanEndpoint Create(string deviceType, int deviceIndex, int channelIndex)
    {
        ValidateIndex(deviceIndex, nameof(deviceIndex));
        ValidateIndex(channelIndex, nameof(channelIndex));

        return CanEndpoint.Create(
            "zlg",
            deviceType,
            channelIndex,
            new Dictionary<string, string>
            {
                ["deviceIndex"] = deviceIndex.ToString(),
            });
    }

    /// <summary>
    /// 创建 ZLG USBCANFD_200U 通道端点。<br/>
    /// Creates a ZLG USBCANFD_200U channel endpoint.
    /// </summary>
    public static CanEndpoint UsbCanFd200U(int deviceIndex, int channelIndex) =>
        Create(ZlgDeviceTypeMap.UsbCanFd200UEndpointName, deviceIndex, channelIndex);

    private static void ValidateIndex(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new CanException(
                "zlg",
                CanErrorCategory.InvalidEndpoint,
                $"ZLG endpoint parameter '{parameterName}' must be non-negative.");
        }
    }
}
