namespace CanHub.Adapter.Vector;

/// <summary>
/// Vector 端点构造辅助工具。用于固定设备配置场景，扫描结果仍优先使用 <see cref="CanChannelInfo.CanonicalEndpoint"/>。<br/>
/// Vector endpoint builder. Use for fixed device configurations; scanned channels should still prefer <see cref="CanChannelInfo.CanonicalEndpoint"/>.
/// </summary>
public static class VectorEndpoint
{
    /// <summary>
    /// 创建 Vector 通道端点。<br/>
    /// Creates a Vector channel endpoint.
    /// </summary>
    public static CanEndpoint Create(string deviceName, int deviceIndex, int channelIndex)
    {
        ValidateIndex(deviceIndex, nameof(deviceIndex));
        ValidateIndex(channelIndex, nameof(channelIndex));

        return CanEndpoint.Create(
            "vector",
            deviceName,
            channelIndex,
            new Dictionary<string, string>
            {
                ["deviceIndex"] = deviceIndex.ToString(),
            });
    }

    private static void ValidateIndex(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new CanException(
                "vector",
                CanErrorCategory.InvalidEndpoint,
                $"Vector endpoint parameter '{parameterName}' must be non-negative.");
        }
    }
}
