namespace CanHub;

/// <summary>
/// CAN 帧发送的可选控制参数。null 表示使用适配器默认值。<br/>
/// Optional controls for CAN frame transmission. Null means adapter defaults.
/// </summary>
public sealed class CanTransmitOptions : IEquatable<CanTransmitOptions>
{
    /// <summary>发送模式。<br/>Transmit mode.</summary>
    public CanTransmitMode Mode { get; }

    /// <summary>完成模式。<br/>Completion mode.</summary>
    public CanTransmitCompletion Completion { get; }

    /// <summary>重试策略。<br/>Retry policy.</summary>
    public CanTransmitRetryPolicy RetryPolicy { get; }

    /// <summary>高优先级发送。<br/>High priority transmission.</summary>
    public bool HighPriority { get; }

    private CanTransmitOptions(
        CanTransmitMode mode,
        CanTransmitCompletion completion,
        CanTransmitRetryPolicy retryPolicy,
        bool highPriority)
    {
        Mode = mode;
        Completion = completion;
        RetryPolicy = retryPolicy;
        HighPriority = highPriority;
    }

    /// <summary>使用可选参数创建发送选项。<br/>Create transmit options with optional parameters.</summary>
    public static CanTransmitOptions Create(
        CanTransmitMode mode = CanTransmitMode.Normal,
        CanTransmitCompletion completion = CanTransmitCompletion.SubmitOnly,
        CanTransmitRetryPolicy retryPolicy = default,
        bool highPriority = false) =>
        new(mode, completion, retryPolicy, highPriority);

    /// <summary>判断两个发送选项是否相等。<br/>Determines whether two transmit options are equal.</summary>
    public bool Equals(CanTransmitOptions? other) =>
        other is not null &&
        Mode == other.Mode &&
        Completion == other.Completion &&
        RetryPolicy == other.RetryPolicy &&
        HighPriority == other.HighPriority;

    /// <inheritdoc cref="Equals(CanTransmitOptions?)"/>
    public override bool Equals(object? obj) => obj is CanTransmitOptions other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mode);
        hash.Add(Completion);
        hash.Add(RetryPolicy);
        hash.Add(HighPriority);
        return hash.ToHashCode();
    }

    /// <summary>判断两个发送选项是否相等。<br/>Determines whether two transmit options are equal.</summary>
    public static bool operator ==(CanTransmitOptions? left, CanTransmitOptions? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    /// <summary>判断两个发送选项是否不等。<br/>Determines whether two transmit options are not equal.</summary>
    public static bool operator !=(CanTransmitOptions? left, CanTransmitOptions? right) => !(left == right);
}
