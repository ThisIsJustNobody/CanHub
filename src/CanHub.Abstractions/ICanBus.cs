namespace CanHub;

/// <summary>
/// 已打开的 CAN 总线句柄。提供发送、订阅和状态通知。<br/>
/// Open CAN bus handle. Provides transmission, subscription, and status notification.
/// </summary>
public interface ICanBus : IDisposable, IAsyncDisposable
{
    /// <summary>总线显示名称。<br/>Bus display name.</summary>
    string DisplayName { get; }

    /// <summary>总线是否已打开。<br/>Whether the bus is open.</summary>
    bool IsOpen { get; }

    /// <summary>异步发送单帧。<br/>Asynchronously sends a single frame.</summary>
    /// <remarks>
    /// 此方法仅表示本地提交结果，不代表远端已接收。发送结果通过 <see cref="CanFrameEvent"/> 以匹配的
    /// <see cref="CanFrameEvent.CorrelationId"/> 异步通知。<br/>
    /// This method only indicates local submission result, not remote reception.
    /// Transmission outcome is delivered asynchronously via <see cref="CanFrameEvent"/>
    /// with matching <see cref="CanFrameEvent.CorrelationId"/>.
    /// </remarks>
    ValueTask<CanTransmitSubmissionResult> SendAsync(
        CanFrame frame,
        CanTransmitOptions? options = null,
        CancellationToken ct = default);

    /// <summary>异步批量发送帧。<br/>Asynchronously sends a batch of frames.</summary>
    ValueTask<CanTransmitSubmissionResult[]> SendBatchAsync(
        ReadOnlyMemory<CanFrame> frames,
        CanTransmitOptions? options = null,
        CancellationToken ct = default);

    /// <summary>创建帧订阅。<br/>Creates a frame subscription.</summary>
    ICanSubscription Subscribe(CanSubscriptionOptions options);

    /// <summary>总线状态变更事件。<br/>Bus status change event.</summary>
    event Action<CanStatusEvent>? StatusChanged;
}
