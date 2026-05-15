using CanHub.Adapter.Zlg.Tests.Support;

namespace CanHub.Adapter.Zlg.Tests.Hardware;

[TestClass]
[TestCategory("Hardware")]
public sealed class ZlgLeaseAndReceiveHardwareTests : ZlgCanHubHardwareTestBase
{
    [TestMethod(DisplayName = "Two ZLG devices can repeatedly open all target channels")]
    public async Task TwoDevices_AllTargetChannels_CanOpenAndCloseRepeatedly()
    {
        RequireZlgHardware();

        for (var i = 0; i < 3; i++)
        {
            var registry = CreateZlgRegistry();
            var bus2ClassicDisabled = new CanBusParameters
            {
                IsFd = false,
                ArbitrationBitrate = 500_000,
                TerminationEnabled = false,
            };

            await using var d0c0 = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);
            await using var d1c0 = await OpenZlgAsync(registry, Env.Device1Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);
            await using var d0c1 = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus2Channel, bus2ClassicDisabled, ct: TestContext.CancellationToken);
            await using var d1c1 = await OpenZlgAsync(registry, Env.Device1Index, Env.Bus2Channel, bus2ClassicDisabled, ct: TestContext.CancellationToken);

            Assert.IsTrue(d0c0.IsOpen);
            Assert.IsTrue(d1c0.IsOpen);
            Assert.IsTrue(d0c1.IsOpen);
            Assert.IsTrue(d1c1.IsOpen);
            TestContext.WriteLine($"Iteration {i + 1}: opened two devices and four ZLG channels through CanHub.");
        }
    }

    [TestMethod(DisplayName = "Merged receive routes CH0 and CH1 by ZCANDataObj channel")]
    public async Task MergedReceive_RoutesBus1AndBus2ToCorrectChannelHubs()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        var bus2FdEnabled = new CanBusParameters
        {
            IsFd = true,
            ArbitrationBitrate = 500_000,
            DataBitrate = 2_000_000,
            TerminationEnabled = true,
        };

        await using var d0Bus1 = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);
        await using var d0Bus2 = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus2Channel, bus2FdEnabled, ct: TestContext.CancellationToken);
        await using var d1Bus1 = await OpenZlgAsync(registry, Env.Device1Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);
        await using var d1Bus2 = await OpenZlgAsync(registry, Env.Device1Index, Env.Bus2Channel, bus2FdEnabled, ct: TestContext.CancellationToken);
        using var bus1Sub = d0Bus1.Subscribe(new CanSubscriptionOptions());
        using var bus2Sub = d0Bus2.Subscribe(new CanSubscriptionOptions());

        var bus1Frame = CanFrame.CreateData(CanId.Standard(0x611), [0x61, 0x01]);
        var bus2Frame = CanFrame.CreateData(CanId.Standard(0x612), [0x62, 0x02]);
        Assert.IsTrue((await d1Bus1.SendAsync(bus1Frame, ct: TestContext.CancellationToken)).Accepted);
        Assert.IsTrue((await d1Bus2.SendAsync(bus2Frame, ct: TestContext.CancellationToken)).Accepted);

        var bus1Received = await WaitForFrameAsync(
            bus1Sub,
            candidate => candidate.Frame.Id.Value == bus1Frame.Id.Value,
            TimeSpan.FromSeconds(2));
        var bus2Received = await WaitForFrameAsync(
            bus2Sub,
            candidate => candidate.Frame.Id.Value == bus2Frame.Id.Value,
            TimeSpan.FromSeconds(2));

        Assert.AreEqual((int)Env.Bus1Channel, bus1Received.ChannelIndex);
        Assert.AreEqual((int)Env.Bus2Channel, bus2Received.ChannelIndex);
        CollectionAssert.AreEqual(CopyPayload(bus1Frame), CopyPayload(bus1Received.Frame));
        CollectionAssert.AreEqual(CopyPayload(bus2Frame), CopyPayload(bus2Received.Frame));
    }

    [TestMethod(DisplayName = "Same ZLG channel reuses matching lease and rejects different config")]
    public async Task SameChannel_ReusesMatchingLeaseAndRejectsDifferentConfig()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        await using var first = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);
        await using var second = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);

        Assert.IsTrue(first.IsOpen);
        Assert.IsTrue(second.IsOpen);

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Fd500k2M, ct: TestContext.CancellationToken));
        Assert.AreEqual(CanErrorCategory.ConfigurationConflict, ex.Category);
    }

    [TestMethod(DisplayName = "Same ZLG device rejects mixed receive strategy")]
    public async Task SameDevice_RejectsMixedReceiveStrategy()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        await using var merged = await OpenZlgAsync(
            registry,
            Env.Device0Index,
            Env.Bus1Channel,
            CanBusParameters.Classic500k,
            new ZlgOpenOptions { UseMergedReceive = true },
            TestContext.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await OpenZlgAsync(
                registry,
                Env.Device0Index,
                Env.Bus2Channel,
                new CanBusParameters
                {
                    IsFd = false,
                    ArbitrationBitrate = 500_000,
                    TerminationEnabled = false,
                },
                new ZlgOpenOptions { UseMergedReceive = false },
                TestContext.CancellationToken));

        Assert.IsTrue(merged.IsOpen);
        Assert.AreEqual(CanErrorCategory.ConfigurationConflict, ex.Category);
    }
}
