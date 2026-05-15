namespace CanHub;

/// <summary>
/// 订阅队列配置选项。<br/>
/// Subscription queue configuration options.
/// </summary>
public sealed class CanSubscriptionOptions
{
    private int _queueCapacity = 4096;

    /// <summary>有界队列容量（必须为正数，0 或负数将使用默认值 4096）。<br/>Bounded queue capacity (must be positive; 0 or negative uses the default of 4096).</summary>
    public int QueueCapacity
    {
        get => _queueCapacity;
        set => _queueCapacity = value > 0 ? value : 4096;
    }

    /// <summary>队列满时的丢帧策略。<br/>Frame drop policy when the queue is full.</summary>
    public CanQueueFullMode FullMode { get; set; } = CanQueueFullMode.DropOldest;
}
