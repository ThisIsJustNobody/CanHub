namespace CanHub;

/// <summary>
/// CAN 发送模式。<br/>
/// CAN transmit mode.
/// </summary>
public enum CanTransmitMode : byte
{
    /// <summary>普通发送。<br/>Normal transmission.</summary>
    Normal = 0,

    /// <summary>单次发送（错误时不自动重试）。<br/>Single-shot transmission (no automatic retry on error).</summary>
    SingleShot = 1,

    /// <summary>自收发送（发送的帧同时也会在总线上接收）。<br/>Transmit with self-reception (sent frame is also received on the bus).</summary>
    SelfReception = 2,

    /// <summary>单次自收发送。<br/>Single-shot with self-reception.</summary>
    SingleShotSelfReception = 3
}
