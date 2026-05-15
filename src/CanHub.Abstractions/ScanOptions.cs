namespace CanHub;

/// <summary>
/// CAN 通道扫描配置。<br/>
/// CAN channel scan configuration.
/// </summary>
public sealed class ScanOptions
{
    private int _minDepth;
    private int _startIndex;

    /// <summary>
    /// 强制最小扫描深度。为 0 时使用自动深度：从 StartIndex 开始，发现设备就继续下一个，直到连续未发现。<br/>
    /// Forced minimum scan depth. When 0, auto-depth is used: starting from StartIndex,
    /// continue to the next index when a device is found, stopping after consecutive misses.
    /// </summary>
    public int MinDepth
    {
        get => _minDepth;
        set => _minDepth = Math.Max(0, value);
    }

    /// <summary>起始设备 index，默认 0。<br/>Starting device index, default 0.</summary>
    public int StartIndex
    {
        get => _startIndex;
        set => _startIndex = Math.Max(0, value);
    }
}
