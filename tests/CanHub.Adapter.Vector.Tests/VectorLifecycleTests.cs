using System.Collections.Concurrent;
using System.Diagnostics;
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

    [TestMethod(DisplayName = "Vector ResetOnFault关闭并重开端口")]
    public async Task Recovery_ResetOnFault_ClosesAndReopensPort()
    {
        var lifecycle = new FakeVectorChannelLifecycle();
        var entry = CreateLeaseEntry(
            portHandle: 10,
            recovery: CanRecoveryOptions.ResetOnFault(restartDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        entry.HandleFaultStatus(CreateBusOffStatus(entry.Port.LogicalChannelIndex));

        await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        CollectionAssert.AreEqual(
            new[] { CanStatusCode.BusOff, CanStatusCode.Recovering, CanStatusCode.Recovered },
            statuses.Select(static status => status.Code).ToArray());
        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(1, lifecycle.OpenCalls);
        Assert.IsTrue(entry.IsOpen);
    }

    [TestMethod(DisplayName = "Vector ReopenWithBackoff按次数重试后恢复")]
    public async Task Recovery_ReopenWithBackoff_RetriesUntilOpenSucceeds()
    {
        var lifecycle = new FakeVectorChannelLifecycle
        {
            FailOpenAttempts = 2,
        };
        var entry = CreateLeaseEntry(
            portHandle: 11,
            recovery: CanRecoveryOptions.ReopenWithBackoff(
                restartDelay: TimeSpan.Zero,
                maxAttempts: 3,
                maxBackoffDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        entry.HandleFaultStatus(CreateBusOffStatus(entry.Port.LogicalChannelIndex));

        var recovered = await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(3, lifecycle.OpenCalls);
        Assert.AreEqual(3ul, recovered.Count);
        Assert.IsTrue(entry.IsOpen);
    }

    [TestMethod(DisplayName = "Vector原生接收错误可按NativeReceiveFault触发恢复")]
    public async Task Recovery_NativeReceiveFault_TriggersWhenConfigured()
    {
        var lifecycle = new FakeVectorChannelLifecycle();
        var entry = CreateLeaseEntry(
            portHandle: 12,
            recovery: CanRecoveryOptions.ResetOnFault(
                triggers: CanRecoveryTrigger.NativeReceiveFault,
                restartDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        entry.HandleFaultStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Error,
            channelIndex: entry.Port.LogicalChannelIndex,
            nativeStatusCode: 1,
            nativeErrorCode: 0x55,
            message: "Synthetic Vector receive error."));

        await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(1, lifecycle.OpenCalls);
        Assert.IsTrue(entry.IsOpen);
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

    private static VectorChannelLeaseEntry CreateLeaseEntry(
        int portHandle = -1,
        CanRecoveryOptions? recovery = null,
        IVectorChannelLifecycle? lifecycle = null)
    {
        var driver = new VectorDriver();
        var port = new VectorChannelPort(driver, channelMask: 1, logicalChannelIndex: 0);
        SetPortHandle(port, portHandle);
        if (portHandle >= 0)
            SetNotificationHandle(port, 0);

        var endpoint = CanEndpoint.Parse("vector://virtual?deviceIndex=0&channelIndex=0");
        var context = new CanOpenContext(endpoint, new CanOpenOptions { BusParameters = CanBusParameters.Classic500k });
        var openSpec = new VectorChannelOpenSpec(context);
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
                openSpec,
                recovery ?? CanRecoveryOptions.Disabled,
                lifecycle ?? VectorNativeChannelLifecycle.Instance,
            ])!;
    }

    private static void SetPortHandle(VectorChannelPort port, int handle) =>
        typeof(VectorChannelPort)
            .GetField("_portHandle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(port, handle);

    private static void SetNotificationHandle(VectorChannelPort port, int handle) =>
        typeof(VectorChannelPort)
            .GetField("_notificationHandle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(port, handle);

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

    private static CanStatusEvent CreateBusOffStatus(int channelIndex) =>
        CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.BusOff,
            CanStatusSeverity.Critical,
            channelIndex: channelIndex,
            message: "Synthetic Vector bus-off.");

    private static async Task<CanStatusEvent> WaitForStatusAsync(
        ConcurrentQueue<CanStatusEvent> statuses,
        CanStatusCode code)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var match = statuses.FirstOrDefault(status => status.Code == code);
            if (match.IsInitialized)
                return match;

            await Task.Delay(10);
        }

        Assert.Fail($"Timed out waiting for status {code}. Observed: {string.Join(", ", statuses.Select(static s => s.Code))}");
        throw new UnreachableException();
    }

    private sealed class FakeVectorChannelLifecycle : IVectorChannelLifecycle
    {
        private int _nextHandle = 100;

        public int CloseCalls { get; private set; }

        public int OpenCalls { get; private set; }

        public int FailOpenAttempts { get; init; }

        public bool ClosePort(VectorChannelPort port, Action<CanStatusEvent> publishStatus)
        {
            CloseCalls++;
            SetPortHandle(port, -1);
            SetNotificationHandle(port, -1);
            return true;
        }

        public ValueTask OpenPortAsync(
            VectorChannelPort port,
            VectorChannelOpenSpec openSpec,
            CancellationToken ct = default)
        {
            OpenCalls++;
            if (OpenCalls <= FailOpenAttempts)
                throw new CanException("vector", CanErrorCategory.AdapterError, "Synthetic Vector reopen failure.");

            SetPortHandle(port, Interlocked.Increment(ref _nextHandle));
            SetNotificationHandle(port, 0);
            return ValueTask.CompletedTask;
        }
    }
}
