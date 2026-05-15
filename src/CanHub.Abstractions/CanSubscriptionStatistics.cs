namespace CanHub;

/// <summary>
/// CanHub 订阅队列的快照。<br/>
/// Snapshot of a CanHub subscription queue.
/// </summary>
public readonly struct CanSubscriptionStatistics : IEquatable<CanSubscriptionStatistics>
{
    /// <summary>快照捕获时的序列号。<br/>Sequence number at which the snapshot was captured when available.</summary>
    public ulong Sequence { get; }

    /// <summary>应用程序分配的逻辑通道索引。这不是供应商驱动通道号。<br/>Application-assigned logical channel index. This is not a vendor driver channel number.</summary>
    public int ChannelIndex { get; }

    /// <summary>系统时间戳（UTC）。<br/>System timestamp (UTC).</summary>
    public DateTimeOffset SystemTimestampUtc { get; }

    /// <summary>配置的有界队列容量。0 表示未指定。<br/>Configured bounded queue capacity. 0 means unspecified.</summary>
    public int Capacity { get; }

    /// <summary>当前缓冲的条目数。<br/>Current buffered item count.</summary>
    public int BufferedCount { get; }

    /// <summary>总丢弃条目数。<br/>Total dropped item count.</summary>
    public ulong DroppedCount { get; }

    /// <summary>最后丢弃的帧序列号。未知时为 0。<br/>Last dropped frame sequence. 0 when no dropped frame is known.</summary>
    public ulong LastDroppedSequence { get; }

    /// <summary>入队前过滤的总条目数。<br/>Total items filtered before enqueue.</summary>
    public ulong FilteredCount { get; }

    /// <summary>
    /// 写入队列的总帧数（含 DropOldest 模式下被通道内部丢弃的帧）。当前仍在缓冲区内的帧数请使用 <see cref="BufferedCount"/>。<br/>
    /// Total frames enqueued (includes frames dropped internally by the channel in DropOldest mode).
    /// Use <see cref="BufferedCount"/> for frames currently still in the buffer.
    /// </summary>
    public ulong EnqueuedCount { get; }

    /// <summary>订阅者读取的总条目数。<br/>Total items read by the subscriber.</summary>
    public ulong ReadCount { get; }

    /// <summary>是否有条目被丢弃。<br/>Whether any item was dropped.</summary>
    public bool HasDroppedFrames => DroppedCount > 0;

    /// <summary>队列压力（0..1 范围，容量已知时；否则为 0）。<br/>Queue pressure in the 0..1 range when capacity is known; otherwise 0.</summary>
    public double QueuePressure => Capacity > 0 ? (double)BufferedCount / Capacity : 0d;

    private CanSubscriptionStatistics(
        ulong sequence,
        int channelIndex,
        DateTimeOffset systemTimestampUtc,
        int capacity,
        int bufferedCount,
        ulong droppedCount,
        ulong lastDroppedSequence,
        ulong filteredCount,
        ulong enqueuedCount,
        ulong readCount)
    {
        Sequence = sequence;
        ChannelIndex = channelIndex;
        SystemTimestampUtc = systemTimestampUtc;
        Capacity = capacity;
        BufferedCount = bufferedCount;
        DroppedCount = droppedCount;
        LastDroppedSequence = lastDroppedSequence;
        FilteredCount = filteredCount;
        EnqueuedCount = enqueuedCount;
        ReadCount = readCount;
    }

    /// <summary>创建订阅队列统计快照。<br/>Create a subscription queue statistics snapshot.</summary>
    public static CanSubscriptionStatistics Create(
        int capacity,
        int bufferedCount,
        ulong droppedCount = 0,
        ulong lastDroppedSequence = 0,
        ulong filteredCount = 0,
        ulong enqueuedCount = 0,
        ulong readCount = 0,
        ulong sequence = 0,
        int channelIndex = 0,
        DateTimeOffset? systemTimestampUtc = null)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                "Capacity must not be negative.");
        if (bufferedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(bufferedCount), bufferedCount,
                "Buffered count must not be negative.");
        if (capacity > 0 && bufferedCount > capacity)
            throw new ArgumentOutOfRangeException(nameof(bufferedCount), bufferedCount,
                "Buffered count must not exceed capacity when capacity is known.");
        if (channelIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(channelIndex), channelIndex,
                "Channel index must be a non-negative application-assigned logical index.");

        return new(sequence, channelIndex, systemTimestampUtc ?? DateTimeOffset.UtcNow,
            capacity, bufferedCount, droppedCount, lastDroppedSequence,
            filteredCount, enqueuedCount, readCount);
    }

    /// <summary>判断两个订阅统计快照是否相等。<br/>Determines whether two subscription statistics snapshots are equal.</summary>
    public bool Equals(CanSubscriptionStatistics other) =>
        Sequence == other.Sequence &&
        ChannelIndex == other.ChannelIndex &&
        SystemTimestampUtc == other.SystemTimestampUtc &&
        Capacity == other.Capacity &&
        BufferedCount == other.BufferedCount &&
        DroppedCount == other.DroppedCount &&
        LastDroppedSequence == other.LastDroppedSequence &&
        FilteredCount == other.FilteredCount &&
        EnqueuedCount == other.EnqueuedCount &&
        ReadCount == other.ReadCount;

    /// <inheritdoc cref="Equals(CanSubscriptionStatistics)"/>
    public override bool Equals(object? obj) => obj is CanSubscriptionStatistics other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Sequence);
        hash.Add(ChannelIndex);
        hash.Add(SystemTimestampUtc);
        hash.Add(Capacity);
        hash.Add(BufferedCount);
        hash.Add(DroppedCount);
        hash.Add(LastDroppedSequence);
        hash.Add(FilteredCount);
        hash.Add(EnqueuedCount);
        hash.Add(ReadCount);
        return hash.ToHashCode();
    }

    /// <summary>判断两个订阅统计快照是否相等。<br/>Determines whether two subscription statistics snapshots are equal.</summary>
    public static bool operator ==(CanSubscriptionStatistics left, CanSubscriptionStatistics right) =>
        left.Equals(right);

    /// <summary>判断两个订阅统计快照是否不等。<br/>Determines whether two subscription statistics snapshots are not equal.</summary>
    public static bool operator !=(CanSubscriptionStatistics left, CanSubscriptionStatistics right) =>
        !left.Equals(right);
}
