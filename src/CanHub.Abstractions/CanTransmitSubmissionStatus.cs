namespace CanHub;

/// <summary>
/// 发送请求是否被本地队列/驱动接受。<br/>
/// Whether a transmit request was accepted by the local queue/driver.
/// </summary>
public enum CanTransmitSubmissionStatus : byte
{
    /// <summary>未指定状态（默认值）。<br/>No status specified (default).</summary>
    None = 0,

    /// <summary>请求已接受。<br/>Request accepted.</summary>
    Accepted = 1,

    /// <summary>驱动队列已满。<br/>Driver queue is full.</summary>
    QueueFull = 2,

    /// <summary>帧无效。<br/>Frame is invalid.</summary>
    InvalidFrame = 3,

    /// <summary>设备未启动。<br/>Device is not started.</summary>
    NotStarted = 4,

    /// <summary>总线关闭状态。<br/>Bus-off condition.</summary>
    BusOff = 5,

    /// <summary>功能不支持。<br/>Feature not supported.</summary>
    UnsupportedFeature = 6,

    /// <summary>请求已取消。<br/>Request was canceled.</summary>
    Canceled = 7,

    /// <summary>请求超时。<br/>Request timed out.</summary>
    Timeout = 8,

    /// <summary>原生驱动错误。<br/>Native driver error.</summary>
    NativeError = 9
}
