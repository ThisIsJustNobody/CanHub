namespace CanHub;

/// <summary>
/// 适配器设备独占模型。<br/>
/// Adapter device exclusivity model.
/// </summary>
public enum ExclusivityModel : byte
{
    /// <summary>无独占（如 Virtual）。<br/>No exclusivity (e.g., Virtual).</summary>
    None = 0,

    /// <summary>设备级独占（如 ZLG：OpenDevice 只能调一次）。<br/>Device-level exclusivity (e.g., ZLG: OpenDevice can only be called once).</summary>
    DeviceLevel = 1,

    /// <summary>通道级引用计数（如 Vector：重复激活 OK，最后释放才关闭）。<br/>Channel-level reference counting (e.g., Vector: repeated activation OK, only closed on final release).</summary>
    ChannelLevel = 2
}
