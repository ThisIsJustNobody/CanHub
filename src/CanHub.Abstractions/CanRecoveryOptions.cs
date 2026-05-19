namespace CanHub;

/// <summary>
/// CAN 总线自动恢复模式。<br/>
/// CAN bus automatic recovery mode.
/// </summary>
public enum CanRecoveryMode : byte
{
    /// <summary>禁用自动恢复，仅上报状态。<br/>Disable automatic recovery; report status only.</summary>
    Disabled = 0,

    /// <summary>故障后关闭通道。<br/>Close the channel after a fault.</summary>
    CloseOnFault = 1,

    /// <summary>故障后立即执行一次关闭并重开。<br/>Immediately close and reopen once after a fault.</summary>
    ResetOnFault = 2,

    /// <summary>故障后按退避策略关闭并重开。<br/>Close and reopen with a backoff policy after a fault.</summary>
    ReopenWithBackoff = 3
}

/// <summary>
/// CAN 总线自动恢复触发条件。<br/>
/// CAN bus automatic recovery triggers.
/// </summary>
[Flags]
public enum CanRecoveryTrigger : ushort
{
    /// <summary>无触发条件。<br/>No trigger.</summary>
    None = 0,

    /// <summary>CAN 控制器进入 bus-off。<br/>CAN controller entered bus-off.</summary>
    BusOff = 1 << 0,

    /// <summary>CAN 控制器进入 error passive。<br/>CAN controller entered error-passive.</summary>
    ErrorPassive = 1 << 1,

    /// <summary>原生接收路径故障。<br/>Native receive-path fault.</summary>
    NativeReceiveFault = 1 << 2,

    /// <summary>原生发送路径故障。<br/>Native transmit-path fault.</summary>
    NativeTransmitFault = 1 << 3
}

/// <summary>
/// CAN 总线自动恢复配置。默认禁用；非禁用策略必须由调用方显式选择。<br/>
/// CAN bus automatic recovery configuration. Disabled by default; non-disabled
/// policies must be selected explicitly by the caller.
/// </summary>
public sealed class CanRecoveryOptions : IEquatable<CanRecoveryOptions>
{
    /// <summary>禁用自动恢复的共享实例。<br/>Shared disabled recovery instance.</summary>
    public static CanRecoveryOptions Disabled { get; } = new(
        CanRecoveryMode.Disabled,
        CanRecoveryTrigger.None,
        TimeSpan.Zero,
        TimeSpan.Zero,
        maxAttempts: 0,
        TimeSpan.Zero,
        rejectTransmitsWhileRecovering: true);

    private CanRecoveryOptions(
        CanRecoveryMode mode,
        CanRecoveryTrigger triggers,
        TimeSpan faultDwellTime,
        TimeSpan restartDelay,
        int maxAttempts,
        TimeSpan maxBackoffDelay,
        bool rejectTransmitsWhileRecovering)
    {
        Mode = mode;
        Triggers = triggers;
        FaultDwellTime = faultDwellTime;
        RestartDelay = restartDelay;
        MaxAttempts = maxAttempts;
        MaxBackoffDelay = maxBackoffDelay;
        RejectTransmitsWhileRecovering = rejectTransmitsWhileRecovering;
    }

    /// <summary>恢复模式。<br/>Recovery mode.</summary>
    public CanRecoveryMode Mode { get; }

    /// <summary>触发条件。<br/>Recovery triggers.</summary>
    public CanRecoveryTrigger Triggers { get; }

    /// <summary>故障驻留时间，超过后才触发恢复。<br/>Fault dwell time before recovery starts.</summary>
    public TimeSpan FaultDwellTime { get; }

    /// <summary>重开前的初始等待时间。<br/>Initial delay before reopening.</summary>
    public TimeSpan RestartDelay { get; }

    /// <summary>最大恢复尝试次数。<br/>Maximum recovery attempts.</summary>
    public int MaxAttempts { get; }

    /// <summary>退避重试的最大等待时间。<br/>Maximum backoff delay.</summary>
    public TimeSpan MaxBackoffDelay { get; }

    /// <summary>恢复期间是否拒绝发送。<br/>Whether transmits are rejected while recovering.</summary>
    public bool RejectTransmitsWhileRecovering { get; }

    /// <summary>创建故障后关闭通道的恢复策略。<br/>Creates a close-on-fault recovery policy.</summary>
    public static CanRecoveryOptions CloseOnFault(
        CanRecoveryTrigger triggers = CanRecoveryTrigger.BusOff,
        TimeSpan? faultDwellTime = null,
        bool rejectTransmitsWhileRecovering = true) =>
        new(
            CanRecoveryMode.CloseOnFault,
            triggers,
            faultDwellTime ?? TimeSpan.Zero,
            TimeSpan.Zero,
            maxAttempts: 0,
            TimeSpan.Zero,
            rejectTransmitsWhileRecovering);

    /// <summary>创建故障后立即关闭并重开一次的恢复策略。<br/>Creates an immediate single close-and-reopen recovery policy.</summary>
    public static CanRecoveryOptions ResetOnFault(
        CanRecoveryTrigger triggers = CanRecoveryTrigger.BusOff,
        TimeSpan? faultDwellTime = null,
        TimeSpan? restartDelay = null,
        bool rejectTransmitsWhileRecovering = true) =>
        new(
            CanRecoveryMode.ResetOnFault,
            triggers,
            faultDwellTime ?? TimeSpan.Zero,
            restartDelay ?? TimeSpan.FromMilliseconds(200),
            maxAttempts: 1,
            restartDelay ?? TimeSpan.FromMilliseconds(200),
            rejectTransmitsWhileRecovering);

    /// <summary>创建故障后按退避策略关闭并重开的恢复策略。<br/>Creates a close-and-reopen recovery policy with backoff.</summary>
    public static CanRecoveryOptions ReopenWithBackoff(
        CanRecoveryTrigger triggers = CanRecoveryTrigger.BusOff,
        TimeSpan? faultDwellTime = null,
        TimeSpan? restartDelay = null,
        int maxAttempts = 3,
        TimeSpan? maxBackoffDelay = null,
        bool rejectTransmitsWhileRecovering = true) =>
        new(
            CanRecoveryMode.ReopenWithBackoff,
            triggers,
            faultDwellTime ?? TimeSpan.Zero,
            restartDelay ?? TimeSpan.FromMilliseconds(200),
            maxAttempts,
            maxBackoffDelay ?? TimeSpan.FromSeconds(5),
            rejectTransmitsWhileRecovering);

    /// <summary>判断两个恢复选项是否相等。<br/>Determines whether two recovery options are equal.</summary>
    public bool Equals(CanRecoveryOptions? other) =>
        other is not null &&
        Mode == other.Mode &&
        Triggers == other.Triggers &&
        FaultDwellTime == other.FaultDwellTime &&
        RestartDelay == other.RestartDelay &&
        MaxAttempts == other.MaxAttempts &&
        MaxBackoffDelay == other.MaxBackoffDelay &&
        RejectTransmitsWhileRecovering == other.RejectTransmitsWhileRecovering;

    /// <inheritdoc cref="Equals(CanRecoveryOptions?)"/>
    public override bool Equals(object? obj) => obj is CanRecoveryOptions other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mode);
        hash.Add(Triggers);
        hash.Add(FaultDwellTime);
        hash.Add(RestartDelay);
        hash.Add(MaxAttempts);
        hash.Add(MaxBackoffDelay);
        hash.Add(RejectTransmitsWhileRecovering);
        return hash.ToHashCode();
    }

    /// <summary>判断两个恢复选项是否相等。<br/>Determines whether two recovery options are equal.</summary>
    public static bool operator ==(CanRecoveryOptions? left, CanRecoveryOptions? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    /// <summary>判断两个恢复选项是否不等。<br/>Determines whether two recovery options are not equal.</summary>
    public static bool operator !=(CanRecoveryOptions? left, CanRecoveryOptions? right) => !(left == right);
}
