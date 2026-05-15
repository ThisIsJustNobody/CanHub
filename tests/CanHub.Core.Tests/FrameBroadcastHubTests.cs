using System.Threading.Channels;

namespace CanHub.Core.Tests;

[TestClass]
public sealed class FrameBroadcastHubTests
{
    private static CanFrameEvent CreateFrameEvent(ulong sequence = 1)
    {
        var frame = CanFrame.CreateData(CanId.Standard(0x100), [0xAA, 0xBB]);
        return CanFrameEvent.CreateReceived(frame, sequence);
    }

    [TestMethod(DisplayName = "广播投递至单个订阅者")]
    public async Task Broadcast_DeliversToSubscriber()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var options = new CanSubscriptionOptions { QueueCapacity = 16 };
        using var sub = hub.Subscribe(options);

        var evt = CreateFrameEvent(1);
        hub.Broadcast(evt);

        var received = await sub.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(evt, received);
        hub.Dispose();
    }

    [TestMethod(DisplayName = "广播投递至多个订阅者")]
    public async Task Broadcast_DeliversToMultipleSubscribers()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var options = new CanSubscriptionOptions { QueueCapacity = 16 };
        using var sub1 = hub.Subscribe(options);
        using var sub2 = hub.Subscribe(options);

        var evt = CreateFrameEvent(1);
        hub.Broadcast(evt);

        var r1 = await sub1.ReadAsync(TestContext.CancellationToken);
        var r2 = await sub2.ReadAsync(TestContext.CancellationToken);
        Assert.AreEqual(evt, r1);
        Assert.AreEqual(evt, r2);
        hub.Dispose();
    }

    [TestMethod(DisplayName = "队列满时丢弃最旧帧")]
    public async Task Broadcast_DropOldest_DropsWhenFull()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var options = new CanSubscriptionOptions { QueueCapacity = 2, FullMode = CanQueueFullMode.DropOldest };
        using var sub = hub.Subscribe(options);

        hub.Broadcast(CreateFrameEvent(1));
        hub.Broadcast(CreateFrameEvent(2));
        hub.Broadcast(CreateFrameEvent(3)); // should drop frame 1

        var first = await sub.ReadAsync(TestContext.CancellationToken);
        var second = await sub.ReadAsync(TestContext.CancellationToken);

        Assert.AreEqual(2ul, first.Sequence);
        Assert.AreEqual(3ul, second.Sequence);
        Assert.AreEqual(1ul, sub.Statistics.DroppedCount);
        Assert.AreEqual(1ul, sub.Statistics.LastDroppedSequence);
        Assert.AreEqual(3ul, sub.Statistics.EnqueuedCount);
        Assert.AreEqual(2ul, sub.Statistics.ReadCount);
        hub.Dispose();
    }

    [TestMethod(DisplayName = "队列刚好写满时不计丢帧")]
    public void Broadcast_FillingToCapacity_DoesNotCountDrop()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var options = new CanSubscriptionOptions { QueueCapacity = 2, FullMode = CanQueueFullMode.DropOldest };
        using var sub = hub.Subscribe(options);

        hub.Broadcast(CreateFrameEvent(1));
        hub.Broadcast(CreateFrameEvent(2));

        Assert.AreEqual(2, sub.Statistics.BufferedCount);
        Assert.AreEqual(0ul, sub.Statistics.DroppedCount);
        Assert.AreEqual(2ul, sub.Statistics.EnqueuedCount);
        hub.Dispose();
    }

    [TestMethod(DisplayName = "读取帧后统计ReadCount和BufferedCount")]
    public async Task Broadcast_ReadAsync_UpdatesReadStatistics()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var options = new CanSubscriptionOptions { QueueCapacity = 2 };
        using var sub = hub.Subscribe(options);

        hub.Broadcast(CreateFrameEvent(1));
        hub.Broadcast(CreateFrameEvent(2));

        var first = await sub.ReadAsync(TestContext.CancellationToken);

        Assert.AreEqual(1ul, first.Sequence);
        Assert.AreEqual(1, sub.Statistics.BufferedCount);
        Assert.AreEqual(1ul, sub.Statistics.ReadCount);
        Assert.AreEqual(2ul, sub.Statistics.EnqueuedCount);
        hub.Dispose();
    }

    [TestMethod(DisplayName = "并发Dispose不会抛出异常")]
    public void Dispose_CalledConcurrently_DoesNotThrow()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        using var sub = hub.Subscribe(new CanSubscriptionOptions { QueueCapacity = 16 });

        Parallel.For(0, 16, _ => hub.Dispose());

        Assert.AreEqual(0ul, sub.Statistics.DroppedCount);
    }

    [TestMethod(DisplayName = "Subscribe拒绝空Options")]
    public void Subscribe_NullOptions_ThrowsArgumentNullException()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());

        Assert.ThrowsExactly<ArgumentNullException>(() => hub.Subscribe(null!));

        hub.Dispose();
    }

    [TestMethod(DisplayName = "Dispose后Subscribe抛ObjectDisposedException")]
    public void Subscribe_AfterDispose_ThrowsObjectDisposedException()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        hub.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(
            () => hub.Subscribe(new CanSubscriptionOptions()));
    }

    [TestMethod(DisplayName = "Subscribe与Dispose并发不会泄漏可读订阅")]
    public async Task Subscribe_ConcurrentWithDispose_DoesNotLeakOpenSubscription()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var subscriptions = new List<ICanSubscription>();
        var gate = new object();

        Parallel.Invoke(
            () =>
            {
                for (var i = 0; i < 64; i++)
                {
                    try
                    {
                        var subscription = hub.Subscribe(new CanSubscriptionOptions());
                        lock (gate)
                        {
                            subscriptions.Add(subscription);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            },
            hub.Dispose);

        foreach (var subscription in subscriptions)
        {
            await Assert.ThrowsExactlyAsync<System.Threading.Channels.ChannelClosedException>(
                () => subscription.ReadAsync(TestContext.CancellationToken).AsTask());
            subscription.Dispose();
        }
    }

    [TestMethod(DisplayName = "DropNewest 溢出时只计丢弃不计入队")]
    public async Task Broadcast_DropNewest_CountsOnlyDroppedWrite()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var options = new CanSubscriptionOptions { QueueCapacity = 2, FullMode = CanQueueFullMode.DropNewest };
        using var sub = hub.Subscribe(options);

        hub.Broadcast(CreateFrameEvent(1));
        hub.Broadcast(CreateFrameEvent(2));
        hub.Broadcast(CreateFrameEvent(3));

        var first = await sub.ReadAsync(TestContext.CancellationToken);
        var second = await sub.ReadAsync(TestContext.CancellationToken);

        Assert.AreEqual(1ul, first.Sequence);
        Assert.AreEqual(2ul, second.Sequence);
        Assert.AreEqual(1ul, sub.Statistics.DroppedCount);
        Assert.AreEqual(3ul, sub.Statistics.LastDroppedSequence);
        Assert.AreEqual(2ul, sub.Statistics.EnqueuedCount);
        Assert.AreEqual(2ul, sub.Statistics.ReadCount);
        hub.Dispose();
    }

    [TestMethod(DisplayName = "DropNewest 不会将已交给等待读取者的帧计为缓冲占用")]
    public async Task Broadcast_DropNewest_PendingReaderDoesNotLeavePhantomBufferedSlot()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var options = new CanSubscriptionOptions { QueueCapacity = 1, FullMode = CanQueueFullMode.DropNewest };
        using var sub = hub.Subscribe(options);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        var firstRead = sub.ReadAsync(timeout.Token).AsTask();

        hub.Broadcast(CreateFrameEvent(1));
        hub.Broadcast(CreateFrameEvent(2));

        var first = await firstRead;
        var second = await sub.ReadAsync(timeout.Token);

        Assert.AreEqual(1ul, first.Sequence);
        Assert.AreEqual(2ul, second.Sequence);
        Assert.AreEqual(0ul, sub.Statistics.DroppedCount);
        Assert.AreEqual(0, sub.Statistics.BufferedCount);
        Assert.AreEqual(2ul, sub.Statistics.EnqueuedCount);
        Assert.AreEqual(2ul, sub.Statistics.ReadCount);
        hub.Dispose();
    }

    [TestMethod(DisplayName = "序列号单调递增")]
    public void AllocateSequence_IsMonotonicallyIncreasing()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var s1 = hub.AllocateSequence();
        var s2 = hub.AllocateSequence();
        var s3 = hub.AllocateSequence();

        Assert.IsLessThan(s2, s1);
        Assert.IsLessThan(s3, s2);
        hub.Dispose();
    }

    [TestMethod(DisplayName = "Dispose后订阅者收到关闭通知")]
    public async Task Dispose_CompletesSubscribers()
    {
        var hub = new FrameBroadcastHub(new CanSequenceGenerator());
        var options = new CanSubscriptionOptions { QueueCapacity = 16 };
        var sub = hub.Subscribe(options);

        hub.Dispose();

        await Assert.ThrowsExactlyAsync<ChannelClosedException>(
            () => sub.ReadAsync(TestContext.CancellationToken).AsTask());

        sub.Dispose();
    }

    public TestContext TestContext { get; set; }
}
