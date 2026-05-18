using CanHub.Adapter.Vector.Internal;
using CanHub.Core;
using Microsoft.Extensions.DependencyInjection;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorRegistrationTests
{
    [TestMethod(DisplayName = "扩展方法可注册Vector适配器")]
    public void RegistrationExtensions_RegisterVectorAdapter()
    {
        var registry = CanHubRegistry.CreateDefault()
            .AddVectorAdapter();

        var adapter = registry.FindAdapter("vector");
        Assert.IsNotNull(adapter);
        Assert.AreEqual("vector", adapter.AdapterId);

        var services = new ServiceCollection();
        services.AddCanHub()
            .AddVectorAdapter();
        using var sp = services.BuildServiceProvider();

        var diRegistry = sp.GetRequiredService<CanHubRegistry>();
        Assert.IsNotNull(diRegistry.FindAdapter("vector"));
    }

    [TestMethod(DisplayName = "VectorOpenOptions指纹反映配置内容")]
    public void VectorOpenOptions_FingerprintReflectsContent()
    {
        var ep = CanEndpoint.Parse("vector://VN1630?channel=0");
        var opts1 = new CanOpenOptions
        {
            NativeOptions = new VectorOpenOptions { ApplicationName = "A", TransmitEcho = false },
        };
        var opts2 = new CanOpenOptions
        {
            NativeOptions = new VectorOpenOptions { ApplicationName = "A", TransmitEcho = true },
        };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts1);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts2);

        Assert.IsFalse(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "Vector拒绝未知NativeOptions类型")]
    public async Task OpenAsync_UnknownNativeOptions_ThrowsConfigurationConflict()
    {
        var provider = new VectorAdapterProvider();
        var context = new CanOpenContext(
            CanEndpoint.Parse("vector://VN1630?channel=0"),
            new CanOpenOptions { NativeOptions = new object() });

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await provider.OpenAsync(context, TestContext.CancellationToken));

        Assert.AreEqual(CanErrorCategory.ConfigurationConflict, ex.Category);
    }

    [TestMethod(DisplayName = "Vector拒绝非法deviceIndex")]
    public async Task OpenAsync_InvalidDeviceIndex_ThrowsInvalidEndpoint()
    {
        await AssertInvalidEndpointAsync("vector://VN1630?deviceIndex=abc&channel=0");
        await AssertInvalidEndpointAsync("vector://VN1630?deviceIndex=-1&channel=0");
    }

    [TestMethod(DisplayName = "Vector拒绝非法channelIndex")]
    public async Task OpenAsync_InvalidChannelIndex_ThrowsInvalidEndpoint()
    {
        await AssertInvalidEndpointAsync("vector://VN1630?deviceIndex=0&channelIndex=abc");
        await AssertInvalidEndpointAsync("vector://VN1630?deviceIndex=0&channelIndex=-1");
    }

    [TestMethod(DisplayName = "Vector拒绝channel与channelIndex冲突")]
    public async Task OpenAsync_ChannelAndChannelIndexConflict_ThrowsInvalidEndpoint()
    {
        await AssertInvalidEndpointAsync("vector://VN1630?deviceIndex=0&channel=1&channelIndex=2");
    }

    [TestMethod(DisplayName = "VectorBus释放时只解绑自身状态事件")]
    public void VectorBus_Dispose_UnsubscribesOnlyOwnStatusHandlers()
    {
        var entry = CreateLeaseEntryForStatusTests();
        using var bus1 = new VectorBus(entry, static _ => { }, static (_, _) => ValueTask.CompletedTask);
        using var bus2 = new VectorBus(entry, static _ => { }, static (_, _) => ValueTask.CompletedTask);
        var bus1Calls = 0;
        var bus2Calls = 0;

        bus1.StatusChanged += _ => bus1Calls++;
        bus2.StatusChanged += _ => bus2Calls++;

        entry.PublishStatus(CreateStatusEvent(1));
        Assert.AreEqual(1, bus1Calls);
        Assert.AreEqual(1, bus2Calls);

        bus1.Dispose();
        entry.PublishStatus(CreateStatusEvent(2));

        Assert.AreEqual(1, bus1Calls);
        Assert.AreEqual(2, bus2Calls);
    }

    [TestMethod(DisplayName = "Vector状态事件handler异常不会阻断其他handler")]
    public void StatusChanged_HandlerThrows_ContinuesOtherHandlers()
    {
        var entry = CreateLeaseEntryForStatusTests();
        var calls = 0;
        entry.StatusChanged += _ => throw new InvalidOperationException("handler failed");
        entry.StatusChanged += _ => calls++;

        entry.PublishStatus(CreateStatusEvent(1));

        Assert.AreEqual(1, calls);
    }

    private async Task AssertInvalidEndpointAsync(string endpoint)
    {
        var provider = new VectorAdapterProvider();
        var context = new CanOpenContext(CanEndpoint.Parse(endpoint), new CanOpenOptions());

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await provider.OpenAsync(context, TestContext.CancellationToken));

        Assert.AreEqual(CanErrorCategory.InvalidEndpoint, ex.Category);
    }

    private static VectorChannelLeaseEntry CreateLeaseEntryForStatusTests()
    {
        var driver = new VectorDriver();
        var port = new VectorChannelPort(driver, channelMask: 1, logicalChannelIndex: 0);
        var coreAssembly = typeof(CanHubRegistry).Assembly;
        var sequenceGeneratorType = coreAssembly.GetType("CanHub.Core.CanSequenceGenerator", throwOnError: true)!;
        var hubType = coreAssembly.GetType("CanHub.Core.FrameBroadcastHub", throwOnError: true)!;
        var sequenceGenerator = Activator.CreateInstance(sequenceGeneratorType, nonPublic: true)!;
        var hub = Activator.CreateInstance(hubType, sequenceGenerator)!;
        var context = new CanOpenContext(
            CanEndpoint.Parse("vector://virtual?deviceIndex=0&channelIndex=0"),
            new CanOpenOptions { BusParameters = CanBusParameters.Classic500k });
        var openSpec = new VectorChannelOpenSpec(context);

        return (VectorChannelLeaseEntry)Activator.CreateInstance(
            typeof(VectorChannelLeaseEntry),
            [
                new VectorChannelKey(XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL, 0, 0),
                driver,
                port,
                hub,
                new byte[32],
                false,
                "Vector status test",
                openSpec,
                CanRecoveryOptions.Disabled,
                VectorNativeChannelLifecycle.Instance,
            ])!;
    }

    private static CanStatusEvent CreateStatusEvent(ulong sequence) =>
        CanStatusEvent.Create(
            CanStatusKind.Driver,
            CanStatusCode.NativeDriverEvent,
            CanStatusSeverity.Info,
            sequence: sequence);

    public TestContext TestContext { get; set; }
}
