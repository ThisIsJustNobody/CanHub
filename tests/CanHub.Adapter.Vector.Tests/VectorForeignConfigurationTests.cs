using System.Collections.Concurrent;
using CanHub.Adapter.Vector.Internal;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
[DoNotParallelize]
public sealed class VectorForeignConfigurationTests
{
    [TestMethod(DisplayName = "VectorOpenOptions 默认忽略外部配置冲突")]
    public void VectorOpenOptions_DefaultsToIgnoreForeignConfiguration()
    {
        var options = new VectorOpenOptions();

        Assert.IsTrue(options.IgnoreForeignConfiguration);
    }

    [TestMethod(DisplayName = "Vector NativeOptions null 与默认 VectorOpenOptions 进程内复用")]
    public async Task OpenAsync_NullNativeOptionsAndDefaultVectorOptions_ShareLease()
    {
        using var hooks = CreateHookScope(new FakeVectorNativeApi());
        var provider = new VectorAdapterProvider();
        var endpoint = CanEndpoint.Parse("vector://virtual?deviceIndex=0&channelIndex=0");

        await using var first = await provider.OpenAsync(
            new CanOpenContext(endpoint, new CanOpenOptions { BusParameters = CanBusParameters.Classic500k }),
            TestContext.CancellationToken);
        await using var second = await provider.OpenAsync(
            new CanOpenContext(endpoint, new CanOpenOptions
            {
                BusParameters = CanBusParameters.Classic500k,
                NativeOptions = new VectorOpenOptions(),
            }),
            TestContext.CancellationToken);

        Assert.IsTrue(first.IsOpen);
        Assert.IsTrue(second.IsOpen);
    }

    [TestMethod(DisplayName = "Vector 默认接入外部已激活 Classic 通道并回放配置警告")]
    public async Task OpenAsync_DefaultOptions_ClassicalInvalidAccess_WarnsAndActivates()
    {
        var native = new FakeVectorNativeApi
        {
            CanSetChannelParamsStatus = XLDefine.XL_Status.XL_ERR_INVALID_ACCESS,
        };
        using var hooks = CreateHookScope(native);
        var provider = new VectorAdapterProvider();

        await using var bus = await provider.OpenAsync(
            new CanOpenContext(
                CanEndpoint.Parse("vector://virtual?deviceIndex=0&channelIndex=1"),
                new CanOpenOptions { BusParameters = CanBusParameters.Classic500k }),
            TestContext.CancellationToken);

        var statuses = new ConcurrentQueue<CanStatusEvent>();
        bus.StatusChanged += statuses.Enqueue;

        Assert.IsTrue(bus.IsOpen);
        Assert.AreEqual(1, native.ActivateChannelCalls);
        Assert.IsTrue(statuses.Any(static status =>
            status.Code == CanStatusCode.ConfigurationIgnored &&
            status.Severity == CanStatusSeverity.Warning &&
            status.NativeStatusCode == (uint)XLDefine.XL_Status.XL_ERR_INVALID_ACCESS &&
            status.Message?.Contains("XL_CanSetChannelParams", StringComparison.Ordinal) == true));
    }

    [TestMethod(DisplayName = "Vector 无 init access 时按外部配置警告并激活")]
    public async Task OpenAsync_DefaultOptions_NoInitAccess_WarnsAndActivates()
    {
        var native = new FakeVectorNativeApi
        {
            GrantInitAccess = false,
        };
        using var hooks = CreateHookScope(native);
        var provider = new VectorAdapterProvider();

        await using var bus = await provider.OpenAsync(
            new CanOpenContext(
                CanEndpoint.Parse("vector://virtual?deviceIndex=0&channelIndex=5"),
                new CanOpenOptions { BusParameters = CanBusParameters.Classic500k }),
            TestContext.CancellationToken);

        var statuses = new ConcurrentQueue<CanStatusEvent>();
        bus.StatusChanged += statuses.Enqueue;

        Assert.IsTrue(bus.IsOpen);
        Assert.AreEqual(1, native.ActivateChannelCalls);
        Assert.AreEqual(0, native.CanSetChannelParamsCalls);
        Assert.IsTrue(statuses.Any(static status =>
            status.Code == CanStatusCode.ConfigurationIgnored &&
            status.Message?.Contains("XL_CanSetChannelParams", StringComparison.Ordinal) == true));
    }

    [TestMethod(DisplayName = "Vector 默认接入外部已激活 FD 通道并回放配置警告")]
    public async Task OpenAsync_DefaultOptions_FdInvalidAccess_WarnsAndActivates()
    {
        var native = new FakeVectorNativeApi
        {
            CanFdSetConfigurationStatus = XLDefine.XL_Status.XL_ERR_INVALID_ACCESS,
        };
        using var hooks = CreateHookScope(native);
        var provider = new VectorAdapterProvider();

        await using var bus = await provider.OpenAsync(
            new CanOpenContext(
                CanEndpoint.Parse("vector://virtual?deviceIndex=0&channelIndex=2"),
                new CanOpenOptions { BusParameters = CanBusParameters.Fd500k2M }),
            TestContext.CancellationToken);

        var statuses = new ConcurrentQueue<CanStatusEvent>();
        bus.StatusChanged += statuses.Enqueue;

        Assert.IsTrue(bus.IsOpen);
        Assert.AreEqual(1, native.ActivateChannelCalls);
        Assert.IsTrue(statuses.Any(static status =>
            status.Code == CanStatusCode.ConfigurationIgnored &&
            status.Message?.Contains("XL_CanFdSetConfiguration", StringComparison.Ordinal) == true));
    }

    [TestMethod(DisplayName = "Vector 严格模式下外部配置冲突阻止激活")]
    public async Task OpenAsync_StrictForeignConfiguration_ThrowsConfigurationConflict()
    {
        var native = new FakeVectorNativeApi
        {
            CanFdSetConfigurationStatus = XLDefine.XL_Status.XL_ERR_INVALID_ACCESS,
        };
        using var hooks = CreateHookScope(native);
        var provider = new VectorAdapterProvider();

        var ex = await Assert.ThrowsExactlyAsync<CanException>(async () =>
            await provider.OpenAsync(
                new CanOpenContext(
                    CanEndpoint.Parse("vector://virtual?deviceIndex=0&channelIndex=3"),
                    new CanOpenOptions
                    {
                        BusParameters = CanBusParameters.Fd500k2M,
                        NativeOptions = new VectorOpenOptions { IgnoreForeignConfiguration = false },
                    }),
                TestContext.CancellationToken));

        Assert.AreEqual(CanErrorCategory.ConfigurationConflict, ex.Category);
        Assert.AreEqual("XL_CanFdSetConfiguration", ex.NativeFunction);
        Assert.AreEqual((int)XLDefine.XL_Status.XL_ERR_INVALID_ACCESS, ex.VendorCode);
        Assert.AreEqual(0, native.ActivateChannelCalls);
    }

    [TestMethod(DisplayName = "Vector 忽略的每个配置调用都产生回放警告")]
    public async Task OpenAsync_IgnoredConfigurationCalls_ReplayWarnings()
    {
        var native = new FakeVectorNativeApi
        {
            CanSetChannelParamsStatus = XLDefine.XL_Status.XL_ERR_INVALID_ACCESS,
            CanSetChannelOutputStatus = XLDefine.XL_Status.XL_ERR_INVALID_ACCESS,
            CanSetChannelModeStatus = XLDefine.XL_Status.XL_ERR_INVALID_ACCESS,
            CanSetReceiveModeStatus = XLDefine.XL_Status.XL_ERR_INVALID_ACCESS,
        };
        using var hooks = CreateHookScope(native);
        var provider = new VectorAdapterProvider();
        var busParameters = new CanBusParameters
        {
            ArbitrationBitrate = 500_000,
            AckOff = false,
        };

        await using var bus = await provider.OpenAsync(
            new CanOpenContext(
                CanEndpoint.Parse("vector://virtual?deviceIndex=0&channelIndex=4"),
                new CanOpenOptions { BusParameters = busParameters }),
            TestContext.CancellationToken);

        var statuses = new ConcurrentQueue<CanStatusEvent>();
        bus.StatusChanged += statuses.Enqueue;

        var messages = statuses
            .Where(static status => status.Code == CanStatusCode.ConfigurationIgnored)
            .Select(static status => status.Message ?? string.Empty)
            .ToArray();

        Assert.AreEqual(4, messages.Length);
        CollectionAssert.Contains(messages, messages.Single(static message => message.Contains("XL_CanSetChannelParams", StringComparison.Ordinal)));
        CollectionAssert.Contains(messages, messages.Single(static message => message.Contains("XL_CanSetChannelOutput", StringComparison.Ordinal)));
        CollectionAssert.Contains(messages, messages.Single(static message => message.Contains("XL_CanSetChannelMode", StringComparison.Ordinal)));
        CollectionAssert.Contains(messages, messages.Single(static message => message.Contains("XL_CanSetReceiveMode", StringComparison.Ordinal)));
    }

    public TestContext TestContext { get; set; } = null!;

    private static IDisposable CreateHookScope(FakeVectorNativeApi native)
    {
        var nativeHooks = VectorDriver.UseNativeApiForTesting(native);
        var lifecycleHooks = VectorDriver.UseLifecycleHooksForTesting(
            openDriver: () => XLDefine.XL_Status.XL_SUCCESS,
            closeDriver: () => XLDefine.XL_Status.XL_SUCCESS,
            getErrorString: static status => status.ToString());

        return new CompositeDisposable(lifecycleHooks, nativeHooks);
    }

    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            for (var i = disposables.Length - 1; i >= 0; i--)
                disposables[i].Dispose();
        }
    }

    private sealed class FakeVectorNativeApi : IVectorNativeApi
    {
        private int _nextPortHandle = 100;

        public XLDefine.XL_Status CanFdSetConfigurationStatus { get; init; } = XLDefine.XL_Status.XL_SUCCESS;

        public XLDefine.XL_Status CanSetChannelParamsStatus { get; init; } = XLDefine.XL_Status.XL_SUCCESS;

        public XLDefine.XL_Status CanSetChannelOutputStatus { get; init; } = XLDefine.XL_Status.XL_SUCCESS;

        public XLDefine.XL_Status CanSetChannelModeStatus { get; init; } = XLDefine.XL_Status.XL_SUCCESS;

        public XLDefine.XL_Status CanSetReceiveModeStatus { get; init; } = XLDefine.XL_Status.XL_SUCCESS;

        public bool GrantInitAccess { get; init; } = true;

        public int ActivateChannelCalls { get; private set; }

        public int CanSetChannelParamsCalls { get; private set; }

        public string GetErrorString(XLDefine.XL_Status status) => status.ToString();

        public ulong GetChannelMask(XLDefine.XL_HardwareType deviceType, int deviceIndex, int channelIndex) =>
            1UL << channelIndex;

        public XLDefine.XL_Status OpenPort(
            ref int portHandle,
            string userName,
            ulong accessMask,
            ref ulong permissionMask,
            uint rxQueueSize,
            XLDefine.XL_InterfaceVersion interfaceVersion,
            XLDefine.XL_BusTypes busType)
        {
            portHandle = Interlocked.Increment(ref _nextPortHandle);
            permissionMask = GrantInitAccess ? accessMask : 0;
            return XLDefine.XL_Status.XL_SUCCESS;
        }

        public XLDefine.XL_Status CanFdSetConfiguration(
            int portHandle,
            ulong accessMask,
            XLClass.XLcanFdConf fdConf) => CanFdSetConfigurationStatus;

        public XLDefine.XL_Status CanSetChannelParams(
            int portHandle,
            ulong accessMask,
            XLClass.xl_chip_params chipParams)
        {
            CanSetChannelParamsCalls++;
            return CanSetChannelParamsStatus;
        }

        public XLDefine.XL_Status CanSetChannelOutput(
            int portHandle,
            ulong accessMask,
            XLDefine.XL_OutputMode outputMode) => CanSetChannelOutputStatus;

        public XLDefine.XL_Status CanSetChannelMode(
            int portHandle,
            ulong accessMask,
            uint tx,
            uint txRq) => CanSetChannelModeStatus;

        public XLDefine.XL_Status CanSetReceiveMode(
            int portHandle,
            byte errorFrame,
            byte chipState) => CanSetReceiveModeStatus;

        public XLDefine.XL_Status SetNotification(int portHandle, ref int handle, int queueLevel)
        {
            handle = 0;
            return XLDefine.XL_Status.XL_SUCCESS;
        }

        public XLDefine.XL_Status ActivateChannel(
            int portHandle,
            ulong accessMask,
            XLDefine.XL_BusTypes busType,
            XLDefine.XL_AC_Flags flags)
        {
            ActivateChannelCalls++;
            return XLDefine.XL_Status.XL_SUCCESS;
        }

        public XLDefine.XL_Status DeactivateChannel(int portHandle, ulong accessMask) =>
            XLDefine.XL_Status.XL_SUCCESS;

        public XLDefine.XL_Status ClosePort(int portHandle) => XLDefine.XL_Status.XL_SUCCESS;

        public XLDefine.XL_Status ReceiveClassic(int portHandle, ref XLClass.xl_event ev) =>
            XLDefine.XL_Status.XL_ERR_QUEUE_IS_EMPTY;

        public XLDefine.XL_Status CanReceive(int portHandle, ref XLClass.XLcanRxEvent rx) =>
            XLDefine.XL_Status.XL_ERR_QUEUE_IS_EMPTY;

        public XLDefine.XL_Status CanTransmit(int portHandle, ulong accessMask, XLClass.xl_event ev) =>
            XLDefine.XL_Status.XL_SUCCESS;

        public XLDefine.XL_Status CanTransmitEx(
            int portHandle,
            ulong accessMask,
            ref uint sent,
            XLClass.XLcanTxEvent tx)
        {
            sent = 1;
            return XLDefine.XL_Status.XL_SUCCESS;
        }
    }
}
