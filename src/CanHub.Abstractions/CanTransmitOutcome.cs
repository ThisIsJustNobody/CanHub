namespace CanHub;

/// <summary>
/// CAN 帧发送结果。<br/>
/// Outcome of a CAN frame transmission.
/// </summary>
public enum CanTransmitOutcome : byte
{
    /// <summary>未指定结果（默认值）。<br/>No outcome specified (default).</summary>
    None = 0,

    /// <summary>帧发送成功。<br/>Frame was successfully transmitted.</summary>
    Transmitted = 1,

    /// <summary>发送失败。<br/>Transmission failed.</summary>
    Failed = 2,

    /// <summary>发送已取消。<br/>Transmission was canceled.</summary>
    Canceled = 3,

    /// <summary>发送超时。<br/>Transmission timed out.</summary>
    TimedOut = 4,

    /// <summary>驱动队列已满。<br/>Driver queue was full.</summary>
    QueueFull = 5,

    /// <summary>总线关闭状态。<br/>Bus-off condition.</summary>
    BusOff = 6,

    /// <summary>功能不支持。<br/>Feature not supported.</summary>
    UnsupportedFeature = 7,

    /// <summary>原生驱动错误。<br/>Native driver error.</summary>
    NativeError = 8,

    /// <summary>发送确认不可用。<br/>Confirmation not available.</summary>
    ConfirmationUnavailable = 9
}
