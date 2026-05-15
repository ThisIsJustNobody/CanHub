namespace CanHub;

/// <summary>
/// CAN 帧订阅句柄。提供异步读取和统计信息。<br/>
/// CAN frame subscription handle. Provides asynchronous reading and statistics.
/// </summary>
public interface ICanSubscription : IDisposable
{
    /// <summary>异步读取下一个帧事件。<br/>Asynchronously reads the next frame event.</summary>
    ValueTask<CanFrameEvent> ReadAsync(CancellationToken ct = default);

    /// <summary>异步枚举所有帧事件。<br/>Asynchronously enumerates all frame events.</summary>
    IAsyncEnumerable<CanFrameEvent> ReadAllAsync(CancellationToken ct = default);

    /// <summary>当前订阅队列统计快照。<br/>Current subscription queue statistics snapshot.</summary>
    CanSubscriptionStatistics Statistics { get; }
}
