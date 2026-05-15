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

    private static ZlgChannelLeaseEntry CreateSyntheticLeaseEntry(bool mergedReceive)
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

        var hub = CreateHub();
        return (ZlgChannelLeaseEntry)Activator.CreateInstance(
            typeof(ZlgChannelLeaseEntry),
            [
                new ZlgChannelKey(capabilities.DeviceTypeId, DeviceIndex: 0, ChannelIndex: 0),
                device,
                nint.Zero,
                hub,
                new byte[32],
                false,
                ZlgTransmitType.Single,
                "Synthetic ZLG lifecycle lease",
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
