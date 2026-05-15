using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanHub.Abstractions.Tests;

[TestClass]
public class CanSubscriptionStatisticsTests
{
    [TestMethod(DisplayName = "默认统计信息为空且安全")]
    public void Default_IsEmptyAndSafe()
    {
        var stats = default(CanSubscriptionStatistics);
        Assert.AreEqual(0, stats.Capacity);
        Assert.AreEqual(0, stats.BufferedCount);
        Assert.AreEqual(0ul, stats.DroppedCount);
        Assert.IsFalse(stats.HasDroppedFrames);
        Assert.AreEqual(0d, stats.QueuePressure);
    }

    [TestMethod(DisplayName = "创建带各项计数的统计信息")]
    public void Create_WithCounts()
    {
        var stats = CanSubscriptionStatistics.Create(
            capacity: 100,
            bufferedCount: 25,
            droppedCount: 3,
            lastDroppedSequence: 42,
            filteredCount: 4,
            enqueuedCount: 50,
            readCount: 20,
            sequence: 60,
            channelIndex: 2);

        Assert.AreEqual(100, stats.Capacity);
        Assert.AreEqual(25, stats.BufferedCount);
        Assert.AreEqual(3ul, stats.DroppedCount);
        Assert.AreEqual(42ul, stats.LastDroppedSequence);
        Assert.AreEqual(4ul, stats.FilteredCount);
        Assert.AreEqual(50ul, stats.EnqueuedCount);
        Assert.AreEqual(20ul, stats.ReadCount);
        Assert.AreEqual(60ul, stats.Sequence);
        Assert.AreEqual(2, stats.ChannelIndex);
        Assert.IsTrue(stats.HasDroppedFrames);
        Assert.AreEqual(0.25d, stats.QueuePressure);
    }

    [TestMethod(DisplayName = "容量未知时队列压力返回零")]
    public void Create_QueuePressure_WhenCapacityUnknown_ReturnsZero()
    {
        var stats = CanSubscriptionStatistics.Create(capacity: 0, bufferedCount: 10);
        Assert.AreEqual(0d, stats.QueuePressure);
    }

    [TestMethod(DisplayName = "容量为负时抛出异常")]
    public void Create_NegativeCapacity_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanSubscriptionStatistics.Create(capacity: -1, bufferedCount: 0));
    }

    [TestMethod(DisplayName = "缓冲数为负时抛出异常")]
    public void Create_NegativeBufferedCount_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanSubscriptionStatistics.Create(capacity: 10, bufferedCount: -1));
    }

    [TestMethod(DisplayName = "缓冲数超过已知容量时抛出异常")]
    public void Create_BufferedCountExceedsKnownCapacity_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanSubscriptionStatistics.Create(capacity: 10, bufferedCount: 11));
    }

    [TestMethod(DisplayName = "通道索引为负时抛出异常")]
    public void Create_NegativeChannelIndex_Throws()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(
            () => CanSubscriptionStatistics.Create(capacity: 10, bufferedCount: 1, channelIndex: -1));
    }

    [TestMethod(DisplayName = "相同值的统计信息相等")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = CanSubscriptionStatistics.Create(
            100, 25, 3, 42, 4, 50, 20, 60, 2, timestamp);
        var b = CanSubscriptionStatistics.Create(
            100, 25, 3, 42, 4, 50, 20, 60, 2, timestamp);

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }
}
