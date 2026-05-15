using CanHub.Adapter.Zlg.Internal;
using CanHub.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanHub.Adapter.Zlg.Tests;

[TestClass]
public sealed class ZlgRegistrationTests
{
    [TestMethod(DisplayName = "ZLG registration extensions add adapter to registry and DI")]
    public void RegistrationExtensions_RegisterZlgAdapter()
    {
        var registry = CanHubRegistry.CreateDefault()
            .AddZlgAdapter();

        var adapter = registry.FindAdapter("zlg");
        Assert.IsNotNull(adapter);
        Assert.AreEqual("zlg", adapter.AdapterId);
        Assert.AreEqual(ExclusivityModel.DeviceLevel, adapter.Manifest.Exclusivity);
        CollectionAssert.Contains(adapter.Manifest.EndpointSchemes.ToArray(), "zlg");

        var services = new ServiceCollection();
        services.AddCanHub()
            .AddZlgAdapter();
        using var sp = services.BuildServiceProvider();

        var diRegistry = sp.GetRequiredService<CanHubRegistry>();
        Assert.IsNotNull(diRegistry.FindAdapter("zlg"));
    }

    [TestMethod(DisplayName = "ZlgOpenOptions fingerprint reflects receive strategy")]
    public void ZlgOpenOptions_FingerprintReflectsReceiveStrategy()
    {
        var ep = CanEndpoint.Parse("zlg://USBCANFD_200U?deviceIndex=0&channel=0");
        var opts1 = new CanOpenOptions
        {
            NativeOptions = new ZlgOpenOptions { UseMergedReceive = true },
        };
        var opts2 = new CanOpenOptions
        {
            NativeOptions = new ZlgOpenOptions { UseMergedReceive = false },
        };

        var fp1 = LeaseConflictDetector.ComputeFingerprint(ep, opts1);
        var fp2 = LeaseConflictDetector.ComputeFingerprint(ep, opts2);

        Assert.IsFalse(LeaseConflictDetector.FingerprintsMatch(fp1, fp2));
    }

    [TestMethod(DisplayName = "Default resolved options use verified merged receive for USBCANFD_200U")]
    public void ResolvedOptions_DefaultsToMergedReceiveForUsbCanFd200U()
    {
        var caps = ZlgDeviceTypeMap.Resolve("USBCANFD_200U");

        var resolved = ZlgResolvedOpenOptions.Create(caps, CanBusParameters.Classic500k, nativeOptions: null);

        Assert.IsTrue(resolved.UseMergedReceive);
        Assert.AreEqual(ZlgWorkMode.Normal, resolved.WorkMode);
        Assert.AreEqual(ZlgTransmitType.Single, resolved.DefaultTransmitType);
    }

    [TestMethod(DisplayName = "AckOff and SelfAck map to verified ZLG work mode")]
    public void ResolvedOptions_MapsAckAndSelfAckToWorkMode()
    {
        var caps = ZlgDeviceTypeMap.Resolve("USBCANFD_200U");

        var ackOff = ZlgResolvedOpenOptions.Create(
            caps,
            new CanBusParameters { IsFd = false, ArbitrationBitrate = 500_000, AckOff = true },
            nativeOptions: null);
        var selfAck = ZlgResolvedOpenOptions.Create(
            caps,
            new CanBusParameters { IsFd = false, ArbitrationBitrate = 500_000, SelfAck = true },
            nativeOptions: null);

        Assert.AreEqual(ZlgWorkMode.NotAck, ackOff.WorkMode);
        Assert.AreEqual(ZlgWorkMode.SelfAck, selfAck.WorkMode);
    }

    [TestMethod(DisplayName = "Conflicting Ack/SelfAck and WorkMode throws ConfigurationConflict")]
    public void ResolvedOptions_WorkModeConflictThrows()
    {
        var caps = ZlgDeviceTypeMap.Resolve("USBCANFD_200U");
        var ex = Assert.ThrowsExactly<CanException>(() => ZlgResolvedOpenOptions.Create(
            caps,
            new CanBusParameters { IsFd = false, ArbitrationBitrate = 500_000, AckOff = true },
            new ZlgOpenOptions { WorkMode = ZlgWorkMode.SelfAck }));

        Assert.AreEqual(CanErrorCategory.ConfigurationConflict, ex.Category);
    }

    [TestMethod(DisplayName = "ZLG rejects unknown NativeOptions type before touching native driver")]
    public async Task OpenAsync_UnknownNativeOptions_ThrowsConfigurationConflict()
    {
        var provider = new ZlgAdapterProvider();
        var context = new CanOpenContext(
            CanEndpoint.Parse("zlg://USBCANFD_200U?deviceIndex=0&channel=0"),
            new CanOpenOptions { NativeOptions = new object() });

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await provider.OpenAsync(context, TestContext.CancellationToken));

        Assert.AreEqual(CanErrorCategory.ConfigurationConflict, ex.Category);
    }

    [TestMethod(DisplayName = "ZLG rejects unsupported device before touching native driver")]
    public async Task OpenAsync_UnsupportedDevice_ThrowsInvalidEndpoint()
    {
        var provider = new ZlgAdapterProvider();
        var context = new CanOpenContext(
            CanEndpoint.Parse("zlg://USBCANFD_400U?deviceIndex=0&channel=0"),
            new CanOpenOptions());

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await provider.OpenAsync(context, TestContext.CancellationToken));

        Assert.AreEqual(CanErrorCategory.InvalidEndpoint, ex.Category);
    }

    [TestMethod(DisplayName = "ZLG rejects invalid deviceIndex")]
    public async Task OpenAsync_InvalidDeviceIndex_ThrowsInvalidEndpoint()
    {
        await AssertInvalidEndpointAsync("zlg://USBCANFD_200U?deviceIndex=abc&channel=0");
        await AssertInvalidEndpointAsync("zlg://USBCANFD_200U?deviceIndex=-1&channel=0");
    }

    [TestMethod(DisplayName = "ZLG rejects invalid channelIndex")]
    public async Task OpenAsync_InvalidChannelIndex_ThrowsInvalidEndpoint()
    {
        await AssertInvalidEndpointAsync("zlg://USBCANFD_200U?deviceIndex=0&channelIndex=abc");
        await AssertInvalidEndpointAsync("zlg://USBCANFD_200U?deviceIndex=0&channelIndex=-1");
    }

    [TestMethod(DisplayName = "ZLG rejects channel and channelIndex conflict")]
    public async Task OpenAsync_ChannelAndChannelIndexConflict_ThrowsInvalidEndpoint()
    {
        await AssertInvalidEndpointAsync("zlg://USBCANFD_200U?deviceIndex=0&channel=1&channelIndex=2");
    }

    private async Task AssertInvalidEndpointAsync(string endpoint)
    {
        var provider = new ZlgAdapterProvider();
        var context = new CanOpenContext(CanEndpoint.Parse(endpoint), new CanOpenOptions());

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await provider.OpenAsync(context, TestContext.CancellationToken));

        Assert.AreEqual(CanErrorCategory.InvalidEndpoint, ex.Category);
    }

    public TestContext TestContext { get; set; }
}
