namespace CanHub.Core;

/// <summary>
/// CAN 序列号生成器。提供单调递增的序列号用于帧事件排序。<br/>
/// CAN sequence generator. Provides monotonically increasing sequence numbers for frame event ordering.
/// </summary>
internal sealed class CanSequenceGenerator
{
    private long _sequence;

    /// <summary>分配下一个单调递增的序列号。<br/>Allocates the next monotonically increasing sequence number.</summary>
    public ulong Allocate() => (ulong)Interlocked.Increment(ref _sequence);
}
