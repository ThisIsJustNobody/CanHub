namespace CanHub;

/// <summary>
/// 描述 CAN 错误的可恢复级别，用于上层决定重试策略。<br/>
/// Describes the recoverability level of a CAN error, used by upper layers to determine retry strategy.
/// </summary>
public enum CanRecoverability
{
    /// <summary>不可恢复，必须终止操作。<br/>Not recoverable; the operation must be terminated.</summary>
    Fatal = 0,

    /// <summary>可立即重试。<br/>Can be retried immediately.</summary>
    Retryable = 1,

    /// <summary>需要重置适配器后重试。<br/>Requires resetting the adapter before retrying.</summary>
    Resettable = 2,

    /// <summary>需要关闭并重新打开连接后重试。<br/>Requires closing and reopening the connection before retrying.</summary>
    ReopenRequired = 3
}
