using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using CanHub.Adapter.Vector.Internal;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorLifecycleTests
{
    [TestMethod(DisplayName = "TrackedSubscription并发Dispose只释放内部订阅一次")]
    public void TrackedSubscription_DisposeConcurrently_DisposesInnerOnce()
    {
        var entry = CreateLeaseEntry();
        using var bus = new VectorBus(entry, static _ => { }, static (_, _) => ValueTask.CompletedTask);
        var inner = new CountingSubscription();
        var trackedType = typeof(VectorBus).GetNestedType("TrackedSubscription", BindingFlags.NonPublic)!;
        var tracked = (ICanSubscription)Activator.CreateInstance(
            trackedType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [inner, bus, Guid.NewGuid()],
            culture: null)!;

        Parallel.For(0, 32, _ => tracked.Dispose());

        Assert.AreEqual(1, inner.DisposeCount);
    }

    [TestMethod(DisplayName = "VectorReceiveLoop Stop在未Start时幂等")]
    public void ReceiveLoop_StopBeforeStart_IsIdempotent()
    {
        var loop = CreateReceiveLoop(_ => { });

        Assert.IsTrue(loop.Stop());
        Assert.IsTrue(loop.Stop());
    }

    [TestMethod(DisplayName = "Vector lease进入closing后拒绝新增引用")]
    public void LeaseEntry_MarkClosing_PreventsNewReference()
    {
        var entry = CreateLeaseEntry();

        entry.MarkClosing();

        Assert.IsFalse(entry.TryAddReference());
        Assert.AreEqual(1, entry.ReferenceCount);
        Assert.IsTrue(entry.Dispose());
    }

    [TestMethod(DisplayName = "VectorDriver打开抛异常后允许下一次Acquire重试")]
    [DoNotParallelize]
    public async Task DriverAcquire_OpenThrows_AllowsSubsequentRetry()
    {
        var openCalls = 0;
        using var hooks = VectorDriver.UseLifecycleHooksForTesting(
            openDriver: () =>
            {
                openCalls++;
                if (openCalls == 1)
                    throw new DllNotFoundException("Simulated missing Vector native DLL.");

                return XLDefine.XL_Status.XL_SUCCESS;
            },
            closeDriver: () => XLDefine.XL_Status.XL_SUCCESS);
        var driver = new VectorDriver();

        await Assert.ThrowsExactlyAsync<DllNotFoundException>(async () => await driver.AcquireAsync());

        try
        {
            await driver.AcquireAsync();

            Assert.AreEqual(2, openCalls, "Acquire should retry XL_OpenDriver after a thrown open failure.");
            Assert.IsTrue(driver.IsOpen);
        }
        finally
        {
            driver.Release();
        }
    }

    [TestMethod(DisplayName = "WaitForSingleObject失败会发布诊断并退出")]
    public async Task ReceiveLoop_WaitFailed_PublishesDiagnostic()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("WaitForSingleObject diagnostic test is Windows-only.");

        var statuses = new ConcurrentQueue<CanStatusEvent>();
        var loop = CreateReceiveLoop(statuses.Enqueue, notificationHandle: 12345);

        loop.Start(isFd: false);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (statuses.IsEmpty && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(20, TestContext.CancellationToken);

        Assert.IsTrue(statuses.TryDequeue(out var status), "Expected receive-loop diagnostic status.");
        Assert.AreEqual(CanStatusKind.Receive, status.Kind);
        Assert.AreEqual(CanStatusCode.NativeDriverError, status.Code);
        StringAssert.Contains(status.Message, "notification wait failed");
        Assert.IsTrue(loop.Stop());
    }

    public TestContext TestContext { get; set; } = null!;

    private static VectorReceiveLoop CreateReceiveLoop(Action<CanStatusEvent> publishStatus, int? notificationHandle = null)
    {
        var port = new VectorChannelPort(new VectorDriver(), channelMask: 1, logicalChannelIndex: 0);
        if (notificationHandle.HasValue)
        {
            typeof(VectorChannelPort)
                .GetField("_notificationHandle", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(port, notificationHandle.Value);
        }

        var hub = CreateHub();
        return (VectorReceiveLoop)Activator.CreateInstance(
            typeof(VectorReceiveLoop),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [port, hub, publishStatus, false],
            culture: null)!;
    }

    private static VectorChannelLeaseEntry CreateLeaseEntry()
    {
        var driver = new VectorDriver();
        var port = new VectorChannelPort(driver, channelMask: 1, logicalChannelIndex: 0);
        return (VectorChannelLeaseEntry)Activator.CreateInstance(
            typeof(VectorChannelLeaseEntry),
            [
                new VectorChannelKey(XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL, 0, 0),
                driver,
                port,
                CreateHub(),
                new byte[32],
                false,
                "Vector lifecycle test",
            ])!;
    }

    private static object CreateHub()
    {
        var coreAssembly = typeof(CanHubRegistry).Assembly;
        var sequenceGeneratorType = coreAssembly.GetType("CanHub.Core.CanSequenceGenerator", throwOnError: true)!;
        var hubType = coreAssembly.GetType("CanHub.Core.FrameBroadcastHub", throwOnError: true)!;
        var sequenceGenerator = Activator.CreateInstance(sequenceGeneratorType, nonPublic: true)!;
        return Activator.CreateInstance(hubType, sequenceGenerator)!;
    }

    private sealed class CountingSubscription : ICanSubscription
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public ValueTask<CanFrameEvent> ReadAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<CanFrameEvent> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public CanSubscriptionStatistics Statistics => default;

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }
}
