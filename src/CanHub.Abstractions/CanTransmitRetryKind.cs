namespace CanHub;

/// <summary>
/// CAN 发送重试类型。<br/>
/// Retry kind for CAN transmission.
/// </summary>
public enum CanTransmitRetryKind : byte
{
    /// <summary>不重试。<br/>No retry.</summary>
    None = 0,

    /// <summary>限制重试次数。<br/>Limited number of retries.</summary>
    LimitedRetries = 1,

    /// <summary>在超时之前一直重试。<br/>Retry until a timeout expires.</summary>
    UntilTimeout = 2,

    /// <summary>在被取消之前一直重试。<br/>Retry until canceled.</summary>
    UntilCanceled = 3,

    /// <summary>限制重试次数，同时有总超时时间。<br/>Limited retries with an overall timeout.</summary>
    LimitedRetriesOrTimeout = 4,

    /// <summary>无限重试。<br/>Unlimited retries.</summary>
    Unlimited = 5
}
