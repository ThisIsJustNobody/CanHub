namespace CanHub;

/// <summary>
/// 扫描时观察到的 CAN 通道当前状态。<br/>
/// Current CAN channel state observed during scanning.
/// </summary>
public enum CanChannelAvailability
{
    /// <summary>扫描时未看到通道处于 active/on-bus 状态。<br/>Channel not seen as active/on-bus during scanning.</summary>
    Available = 0,

    /// <summary>驱动报告通道已 active/on-bus；仍可尝试用匹配配置打开。<br/>Driver reports channel as active/on-bus; can still attempt to open with matching configuration.</summary>
    Active = 1,

    /// <summary>该通道不支持 CAN，不能通过 CanHub 打开。<br/>Channel does not support CAN and cannot be opened through CanHub.</summary>
    Unsupported = 2,

    /// <summary>扫描该通道时出现错误。<br/>An error occurred while scanning this channel.</summary>
    Error = 3,
}
