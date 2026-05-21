namespace CanHub.Adapter.Zlg;

/// <summary>
/// ZLG 常用自动恢复策略预设。非禁用策略仍需由调用方显式传入 <see cref="CanOpenOptions.Recovery"/>。<br/>
/// Common ZLG automatic recovery profiles. Non-disabled policies must still be explicitly assigned to <see cref="CanOpenOptions.Recovery"/>.
/// </summary>
public static class ZlgRecoveryProfiles
{
    private const CanRecoveryTrigger BusFaultTriggers =
        CanRecoveryTrigger.BusOff |
        CanRecoveryTrigger.ErrorPassive |
        CanRecoveryTrigger.NativeReceiveFault |
        CanRecoveryTrigger.NativeTransmitFault;

    /// <summary>
    /// 禁用自动恢复，仅上报状态。<br/>
    /// Disables automatic recovery and reports status only.
    /// </summary>
    public static CanRecoveryOptions Disabled { get; } = CanRecoveryOptions.Disabled;

    /// <summary>
    /// 面向常见 ZLG 总线/原生故障的退避重开策略。<br/>
    /// Backoff reopen policy for common ZLG bus and native faults.
    /// </summary>
    public static CanRecoveryOptions BusFaultBackoff { get; } = CanRecoveryOptions.ReopenWithBackoff(
        triggers: BusFaultTriggers,
        restartDelay: TimeSpan.FromMilliseconds(500),
        maxAttempts: 3,
        maxBackoffDelay: TimeSpan.FromSeconds(5));

    /// <summary>
    /// 面向实验台架的保守退避策略，等待更久并允许更多重试。<br/>
    /// Conservative bench policy with longer waits and more retry attempts.
    /// </summary>
    public static CanRecoveryOptions ConservativeBench { get; } = CanRecoveryOptions.ReopenWithBackoff(
        triggers: BusFaultTriggers,
        faultDwellTime: TimeSpan.FromMilliseconds(200),
        restartDelay: TimeSpan.FromSeconds(1),
        maxAttempts: 5,
        maxBackoffDelay: TimeSpan.FromSeconds(10));
}
