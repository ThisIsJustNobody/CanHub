using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg.Tests;

[TestClass]
public sealed class ZlgLifecycleTests
{
    [TestMethod(DisplayName = "TrackedSubscription并发Dispose只释放内部订阅一次")]
    public void TrackedSubscription_DisposeConcurrently_DisposesInnerOnce()
    {
        var entry = CreateSyntheticLeaseEntry(mergedReceive: true);
        using var bus = new ZlgBus(entry, static _ => { }, static (_, _) => ValueTask.CompletedTask);
        var inner = new CountingSubscription();
        var trackedType = typeof(ZlgBus).GetNestedType("TrackedSubscription", BindingFlags.NonPublic)!;
        var tracked = (ICanSubscription)Activator.CreateInstance(
            trackedType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [inner, bus, Guid.NewGuid()],
            culture: null)!;

        Parallel.For(0, 32, _ => tracked.Dispose());

        Assert.AreEqual(1, inner.DisposeCount);
    }

    [TestMethod(DisplayName = "ZLG非合并接收循环Stop在未Start时幂等")]
    public void NonMergedReceiveLoop_StopBeforeStart_IsIdempotent()
    {
        var entry = CreateSyntheticLeaseEntry(mergedReceive: false);
        var loop = new ZlgNonMergedReceiveLoop(entry);

        Assert.IsTrue(loop.Stop());
        Assert.IsTrue(loop.Stop());
    }

    [TestMethod(DisplayName = "ZLG合并接收循环Stop在未Start时幂等")]
    public void MergedReceiveLoop_StopBeforeStart_IsIdempotent()
    {
        var entry = CreateSyntheticLeaseEntry(mergedReceive: true);
        var loop = new ZlgMergedReceiveLoop(entry.Device);

        Assert.IsTrue(loop.Stop());
        Assert.IsTrue(loop.Stop());
    }

    [TestMethod(DisplayName = "ZLG channel lease进入closing后拒绝新增引用")]
    public void ChannelLeaseEntry_MarkClosing_PreventsNewReference()
    {
        var entry = CreateSyntheticLeaseEntry(mergedReceive: true);

        entry.MarkClosing();

        Assert.IsFalse(entry.TryAddReference());
        Assert.AreEqual(1, entry.ReferenceCount);
        Assert.IsTrue(entry.Dispose());
    }

    [TestMethod(DisplayName = "ZLG device lease进入closing后标记不可复用")]
    public void DeviceLeaseEntry_MarkClosing_MarksDeviceUnavailable()
    {
        var entry = CreateSyntheticLeaseEntry(mergedReceive: true);

        entry.Device.MarkClosing();

        Assert.IsTrue(entry.Device.IsClosingOrDisposed);
        Assert.IsTrue(entry.Device.Dispose());
    }

    [TestMethod(DisplayName = "ZLG状态事件handler异常不会阻断其他handler")]
    public void StatusChanged_HandlerThrows_ContinuesOtherHandlers()
    {
        var entry = CreateSyntheticLeaseEntry(mergedReceive: true);
        var calls = 0;
        entry.StatusChanged += _ => throw new InvalidOperationException("handler failed");
        entry.StatusChanged += _ => calls++;

        entry.PublishStatus(CanStatusEvent.Create(
            CanStatusKind.Driver,
            CanStatusCode.NativeDriverEvent,
            CanStatusSeverity.Info));

        Assert.AreEqual(1, calls);
    }

    [TestMethod(DisplayName = "ZLG ResetOnFault关闭并重开通道")]
    public async Task Recovery_ResetOnFault_ClosesAndReopensChannel()
    {
        var lifecycle = new FakeZlgChannelLifecycle();
        var entry = CreateSyntheticLeaseEntry(
            mergedReceive: true,
            channelHandle: 10,
            recovery: CanRecoveryOptions.ResetOnFault(restartDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        entry.HandleFaultStatus(CreateBusOffStatus(entry.Key.ChannelIndex));

        await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        CollectionAssert.AreEqual(
            new[] { CanStatusCode.BusOff, CanStatusCode.Recovering, CanStatusCode.Recovered },
            statuses.Select(static status => status.Code).ToArray());
        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(1, lifecycle.OpenCalls);
        Assert.AreEqual((nint)10, lifecycle.ClosedHandles.Single());
        Assert.IsTrue(entry.IsOpen);
    }

    [TestMethod(DisplayName = "ZLG ReopenWithBackoff按次数重试后恢复")]
    public async Task Recovery_ReopenWithBackoff_RetriesUntilOpenSucceeds()
    {
        var lifecycle = new FakeZlgChannelLifecycle
        {
            FailOpenAttempts = 2,
        };
        var entry = CreateSyntheticLeaseEntry(
            mergedReceive: true,
            channelHandle: 11,
            recovery: CanRecoveryOptions.ReopenWithBackoff(
                restartDelay: TimeSpan.Zero,
                maxAttempts: 3,
                maxBackoffDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        entry.HandleFaultStatus(CreateBusOffStatus(entry.Key.ChannelIndex));

        var recovered = await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(3, lifecycle.OpenCalls);
        Assert.AreEqual(3ul, recovered.Count);
        Assert.IsTrue(entry.IsOpen);
    }

    [TestMethod(DisplayName = "ZLG原生总线错误可按NativeReceiveFault触发恢复")]
    public async Task Recovery_NativeBusError_TriggersWhenNativeReceiveFaultIsConfigured()
    {
        var lifecycle = new FakeZlgChannelLifecycle();
        var entry = CreateSyntheticLeaseEntry(
            mergedReceive: true,
            channelHandle: 12,
            recovery: CanRecoveryOptions.ResetOnFault(
                triggers: CanRecoveryTrigger.NativeReceiveFault,
                restartDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        entry.HandleFaultStatus(CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.NativeDriverError,
            CanStatusSeverity.Warning,
            channelIndex: entry.Key.ChannelIndex,
            nativeStatusCode: (uint)ZlgErrorType.BusError,
            nativeErrorCode: BuildNativeErrorCode(ZlgErrorType.BusError, ZlgBusErrorSubType.AckError, ZlgNodeState.Warning),
            message: "Synthetic ZLG ACK error."));

        await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(1, lifecycle.OpenCalls);
        Assert.IsTrue(entry.IsOpen);
    }

    [TestMethod(DisplayName = "ZLG发送原生错误可按NativeTransmitFault触发恢复")]
    public async Task Recovery_NativeTransmitFault_FromBusSendPath_TriggersRecovery()
    {
        var lifecycle = new FakeZlgChannelLifecycle();
        var entry = CreateSyntheticLeaseEntry(
            mergedReceive: true,
            channelHandle: 13,
            recovery: CanRecoveryOptions.ResetOnFault(
                triggers: CanRecoveryTrigger.NativeTransmitFault,
                restartDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        using var bus = new ZlgBus(entry, static _ => { }, static (_, _) => ValueTask.CompletedTask);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        InvokePublishTransmitError(bus, correlationId: 99, nativeStatusCode: 0, nativeErrorCode: 0, "Synthetic transmit failure.");

        await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(1, lifecycle.OpenCalls);
        Assert.IsTrue(entry.IsOpen);
    }

    [TestMethod(DisplayName = "ZLG非合并接收循环错误可按NativeReceiveFault触发恢复")]
    public async Task Recovery_NonMergedReceiveLoopFault_TriggersRecovery()
    {
        var lifecycle = new FakeZlgChannelLifecycle();
        var entry = CreateSyntheticLeaseEntry(
            mergedReceive: false,
            channelHandle: 14,
            recovery: CanRecoveryOptions.ResetOnFault(
                triggers: CanRecoveryTrigger.NativeReceiveFault,
                restartDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        entry.HandleReceiveLoopFault("Synthetic non-merged receive failure.");

        await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(1, lifecycle.OpenCalls);
        Assert.IsTrue(entry.IsOpen);
    }

    [TestMethod(DisplayName = "ZLG合并接收循环错误可按NativeReceiveFault触发已注册通道恢复")]
    public async Task Recovery_MergedReceiveLoopFault_TriggersRegisteredChannelRecovery()
    {
        var lifecycle = new FakeZlgChannelLifecycle();
        var entry = CreateSyntheticLeaseEntry(
            mergedReceive: true,
            channelHandle: 15,
            recovery: CanRecoveryOptions.ResetOnFault(
                triggers: CanRecoveryTrigger.NativeReceiveFault,
                restartDelay: TimeSpan.Zero),
            lifecycle: lifecycle);
        RegisterRouteForTesting(entry);
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        entry.StatusChanged += statuses.Enqueue;

        entry.Device.HandleReceiveLoopFault("Synthetic merged receive failure.");

        await WaitForStatusAsync(statuses, CanStatusCode.Recovered);

        Assert.AreEqual(1, lifecycle.CloseCalls);
        Assert.AreEqual(1, lifecycle.OpenCalls);
        Assert.IsTrue(entry.IsOpen);
    }

    [TestMethod(DisplayName = "ZLG恢复等待期间释放不会重新打开通道")]
    public async Task Recovery_DisposeDuringRestartDelay_DoesNotReopenChannel()
    {
        var lifecycle = new FakeZlgChannelLifecycle();
        var entry = CreateSyntheticLeaseEntry(
            mergedReceive: true,
            channelHandle: 16,
            recovery: CanRecoveryOptions.ResetOnFault(
                restartDelay: TimeSpan.FromMilliseconds(150)),
            lifecycle: lifecycle);

        entry.HandleFaultStatus(CreateBusOffStatus(entry.Key.ChannelIndex));
        await WaitUntilAsync(() => lifecycle.CloseCalls == 1);
        entry.MarkClosing();
        Assert.IsTrue(entry.Dispose());
        await Task.Delay(250);

        Assert.AreEqual(0, lifecycle.OpenCalls);
        Assert.IsFalse(entry.IsOpen);
    }

    [TestMethod(DisplayName = "ZLG配置恢复中不拒绝发送时保持提交允许")]
    public async Task Recovery_RejectTransmitsFalse_AllowsSubmitWhileHandleIsStillOpen()
    {
        var entry = CreateSyntheticLeaseEntry(
            mergedReceive: true,
            channelHandle: 17,
            recovery: CanRecoveryOptions.ResetOnFault(
                faultDwellTime: TimeSpan.FromMilliseconds(150),
                restartDelay: TimeSpan.Zero,
                rejectTransmitsWhileRecovering: false),
            lifecycle: new FakeZlgChannelLifecycle());

        entry.HandleFaultStatus(CreateBusOffStatus(entry.Key.ChannelIndex));
        await WaitUntilAsync(() => entry.IsRecovering);

        Assert.IsTrue(entry.CanSubmitTransmit);
    }

    private static ZlgChannelLeaseEntry CreateSyntheticLeaseEntry(
        bool mergedReceive,
        nint channelHandle = 0,
        CanRecoveryOptions? recovery = null,
        IZlgChannelLifecycle? lifecycle = null)
    {
        var capabilities = ZlgDeviceTypeMap.Resolve("USBCANFD_200U");
        var deviceInfo = new ZlgDeviceInfo(
            (ZlgDeviceType)capabilities.DeviceTypeId,
            DeviceIndex: 0,
            HardwareVersion: "0.0",
            FirmwareVersion: "0.0",
            DriverVersion: "0.0",
            InterfaceVersion: "0.0",
            IrqNumber: 0,
            CanChannelCount: (byte)capabilities.DefaultChannelCount,
            SerialNumber: "synthetic",
            HardwareType: capabilities.EndpointName);

        var deviceConstructor = typeof(ZlgDeviceLeaseEntry).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(ZlgDeviceKey),
                typeof(ZlgDeviceCapabilities),
                typeof(nint),
                typeof(ZlgDeviceInfo),
                typeof(bool),
            ],
            modifiers: null)!;
        var device = (ZlgDeviceLeaseEntry)deviceConstructor.Invoke(
        [
            new ZlgDeviceKey(capabilities.DeviceTypeId, DeviceIndex: 0),
            capabilities,
            nint.Zero,
            deviceInfo,
            mergedReceive,
        ]);

        var key = new ZlgChannelKey(capabilities.DeviceTypeId, DeviceIndex: 0, ChannelIndex: 0);
        var busParameters = CanBusParameters.Classic500k;
        var resolved = ZlgResolvedOpenOptions.Create(capabilities, busParameters, null);
        var openSpec = new ZlgChannelOpenSpec(key, busParameters, resolved);
        var hub = CreateHub();
        return (ZlgChannelLeaseEntry)Activator.CreateInstance(
            typeof(ZlgChannelLeaseEntry),
            [
                key,
                device,
                channelHandle,
                hub,
                new byte[32],
                false,
                ZlgTransmitType.Single,
                "Synthetic ZLG lifecycle lease",
                openSpec,
                recovery ?? CanRecoveryOptions.Disabled,
                lifecycle ?? ZlgNativeChannelLifecycle.Instance,
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

    private static CanStatusEvent CreateBusOffStatus(int channelIndex) =>
        CanStatusEvent.Create(
            CanStatusKind.Bus,
            CanStatusCode.BusOff,
            CanStatusSeverity.Critical,
            channelIndex: channelIndex,
            message: "Synthetic ZLG bus-off.");

    private static uint BuildNativeErrorCode(
        ZlgErrorType errorType,
        ZlgBusErrorSubType errorSubType,
        ZlgNodeState nodeState) =>
        ((uint)errorType << 24) |
        ((uint)errorSubType << 16) |
        ((uint)nodeState << 8);

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

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
                return;

            await Task.Delay(10);
        }

        Assert.Fail("Timed out waiting for expected condition.");
    }

    private static void InvokePublishTransmitError(
        ZlgBus bus,
        ulong correlationId,
        uint nativeStatusCode,
        uint nativeErrorCode,
        string message) =>
        typeof(ZlgBus)
            .GetMethod("PublishTransmitError", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(bus, [correlationId, nativeStatusCode, nativeErrorCode, message]);

    private static void RegisterRouteForTesting(ZlgChannelLeaseEntry entry)
    {
        var routes = (ConcurrentDictionary<int, ZlgChannelLeaseEntry>)typeof(ZlgDeviceLeaseEntry)
            .GetField("_routes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(entry.Device)!;
        routes[entry.Key.ChannelIndex] = entry;
    }

    private sealed class FakeZlgChannelLifecycle : IZlgChannelLifecycle
    {
        private int _nextHandle = 100;

        public int CloseCalls { get; private set; }

        public int OpenCalls { get; private set; }

        public int FailOpenAttempts { get; init; }

        public List<nint> ClosedHandles { get; } = [];

        public nint OpenChannel(ZlgDeviceLeaseEntry device, ZlgChannelOpenSpec spec)
        {
            OpenCalls++;
            if (OpenCalls <= FailOpenAttempts)
                throw new ZlgApiException("ZCAN_InitCAN", ZlgStatus.Error);

            return (nint)Interlocked.Increment(ref _nextHandle);
        }

        public bool CloseChannel(nint channelHandle, int channelIndex, Action<CanStatusEvent> publishStatus)
        {
            CloseCalls++;
            ClosedHandles.Add(channelHandle);
            return true;
        }
    }
}
