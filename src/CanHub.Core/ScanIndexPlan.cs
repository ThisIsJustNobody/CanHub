namespace CanHub.Core;

/// <summary>
/// 扫描设备索引的执行计划。把 ScanOptions 的 StartIndex/MinDepth 语义转换为适配器可复用的循环判断。<br/>
/// Device-index scan plan. Converts ScanOptions StartIndex/MinDepth semantics into reusable loop decisions for adapters.
/// </summary>
internal readonly record struct ScanIndexPlan(int StartIndex, int MinimumDepth)
{
    /// <summary>
    /// 从扫描选项创建执行计划。null 选项等价于默认 ScanOptions。<br/>
    /// Creates a scan plan from options. Null options are equivalent to default ScanOptions.
    /// </summary>
    public static ScanIndexPlan FromOptions(ScanOptions? options) =>
        new(options?.StartIndex ?? 0, options?.MinDepth ?? 0);

    /// <summary>
    /// 判断扫描当前索引后是否继续扫描下一个索引。<br/>
    /// Determines whether scanning should continue to the next index after scanning the current index.
    /// </summary>
    public bool ShouldContinueAfter(int scannedCount, bool foundAtCurrentIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scannedCount);

        if (scannedCount < MinimumDepth)
            return true;

        return foundAtCurrentIndex;
    }
}
