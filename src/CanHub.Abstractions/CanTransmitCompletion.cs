namespace CanHub;

/// <summary>
/// CAN 发送完成模式。<br/>
/// CAN transmit completion mode.
/// </summary>
public enum CanTransmitCompletion : byte
{
    /// <summary>仅提交到驱动队列，不等待完成。<br/>Submit to driver queue only, do not wait for completion.</summary>
    SubmitOnly = 0,

    /// <summary>等待控制器发送确认。<br/>Wait for transmit confirmation from the controller.</summary>
    WaitForTransmitConfirmation = 1,

    /// <summary>等待总线上的接收回显。<br/>Wait for receive echo on the bus.</summary>
    WaitForReceiveEcho = 2
}
