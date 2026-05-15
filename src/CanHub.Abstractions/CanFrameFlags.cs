namespace CanHub;

/// <summary>
/// CAN 帧属性标志（可组合）。<br/>
/// CAN frame attribute flags (combinable).
/// </summary>
[Flags]
public enum CanFrameFlags : byte
{
    /// <summary>无特殊标志。<br/>No special flags.</summary>
    None = 0,

    /// <summary>CAN FD 帧。<br/>CAN FD frame.</summary>
    FD = 1 << 0,

    /// <summary>比特率切换（BRS），仅 CAN FD。<br/>Bit rate switch (BRS), CAN FD only.</summary>
    BRS = 1 << 1,

    /// <summary>错误状态指示器（ESI），仅 CAN FD。<br/>Error state indicator (ESI), CAN FD only.</summary>
    ESI = 1 << 2
}
