namespace CanHub;

/// <summary>
/// 队列满时的丢帧策略。<br/>
/// Frame drop strategy when the queue is full.
/// </summary>
public enum CanQueueFullMode : byte
{
    /// <summary>丢弃最旧的帧。<br/>Drop the oldest frame.</summary>
    DropOldest = 0,

    /// <summary>丢弃最新的帧。<br/>Drop the newest frame.</summary>
    DropNewest = 1
}
