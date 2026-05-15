namespace CanHub;

/// <summary>
/// CAN 发送重试策略。只读结构体，值语义。<br/>
/// Retry policy for CAN transmission. Readonly struct with value semantics.
/// </summary>
public readonly struct CanTransmitRetryPolicy : IEquatable<CanTransmitRetryPolicy>
{
    /// <summary>重试类型。<br/>Retry kind.</summary>
    public CanTransmitRetryKind Kind { get; }

    /// <summary>最大重试次数。由 LimitedRetries 和 LimitedRetriesOrTimeout 使用。<br/>Maximum retry count. Used by LimitedRetries and LimitedRetriesOrTimeout.</summary>
    public int MaxRetryCount { get; }

    /// <summary>超时时间。由 UntilTimeout 和 LimitedRetriesOrTimeout 使用。<br/>Timeout. Used by UntilTimeout and LimitedRetriesOrTimeout.</summary>
    public TimeSpan Timeout { get; }

    private CanTransmitRetryPolicy(CanTransmitRetryKind kind, int maxRetryCount, TimeSpan timeout)
    {
        Kind = kind;
        MaxRetryCount = maxRetryCount;
        Timeout = timeout;
    }

    /// <summary>默认无重试策略。<br/>No retry (default).</summary>
    public static CanTransmitRetryPolicy None => new(CanTransmitRetryKind.None, 0, TimeSpan.Zero);

    /// <summary>无重试。<br/>No retry.</summary>
    public static CanTransmitRetryPolicy NoRetry() => None;

    /// <summary>限制重试次数。值 0 表示仅尝试一次。<br/>Limited number of retries. A value of 0 means try once with no retry.</summary>
    public static CanTransmitRetryPolicy LimitedRetries(int maxRetryCount)
    {
        if (maxRetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), maxRetryCount,
                "Retry count must not be negative.");
        return new(CanTransmitRetryKind.LimitedRetries, maxRetryCount, TimeSpan.Zero);
    }

    /// <summary>重试直到超时。<br/>Retry until timeout expires.</summary>
    public static CanTransmitRetryPolicy UntilTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                "Timeout must be positive for timeout-based retry policies.");
        return new(CanTransmitRetryKind.UntilTimeout, 0, timeout);
    }

    /// <summary>重试直到取消。<br/>Retry until canceled.</summary>
    public static CanTransmitRetryPolicy UntilCanceled() =>
        new(CanTransmitRetryKind.UntilCanceled, 0, TimeSpan.Zero);

    /// <summary>限制重试次数且设置总体超时。<br/>Limited retries with an overall timeout.</summary>
    public static CanTransmitRetryPolicy LimitedRetriesOrTimeout(int maxRetryCount, TimeSpan timeout)
    {
        if (maxRetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), maxRetryCount,
                "Retry count must not be negative.");
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                "Timeout must be positive for timeout-based retry policies.");
        return new(CanTransmitRetryKind.LimitedRetriesOrTimeout, maxRetryCount, timeout);
    }

    /// <summary>无限重试。<br/>Unlimited retries.</summary>
    public static CanTransmitRetryPolicy Unlimited() =>
        new(CanTransmitRetryKind.Unlimited, 0, TimeSpan.Zero);

    /// <summary>判断两个重试策略是否相等。<br/>Determines whether two retry policies are equal.</summary>
    public bool Equals(CanTransmitRetryPolicy other) =>
        Kind == other.Kind &&
        MaxRetryCount == other.MaxRetryCount &&
        Timeout == other.Timeout;

    /// <inheritdoc cref="Equals(CanTransmitRetryPolicy)"/>
    public override bool Equals(object? obj) => obj is CanTransmitRetryPolicy other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(MaxRetryCount);
        hash.Add(Timeout);
        return hash.ToHashCode();
    }

    /// <summary>判断两个重试策略是否相等。<br/>Determines whether two retry policies are equal.</summary>
    public static bool operator ==(CanTransmitRetryPolicy left, CanTransmitRetryPolicy right) => left.Equals(right);

    /// <summary>判断两个重试策略是否不等。<br/>Determines whether two retry policies are not equal.</summary>
    public static bool operator !=(CanTransmitRetryPolicy left, CanTransmitRetryPolicy right) => !left.Equals(right);
}
