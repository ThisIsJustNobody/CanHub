using CanHub;
using CanHub.Adapter.Vector;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorVirtualBusTests
{
    [TestInitialize]
    public void CheckHardwareAvailable()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CANHUB_TEST_VECTOR")))
            Assert.Inconclusive("Skipping: CANHUB_TEST_VECTOR is not set.");
    }

    private CanHubRegistry CreateRegistry()
    {
        var registry = CanHubRegistry.CreateDefault();
        registry.AddVectorAdapter();
        return registry;
    }

    [TestMethod]
    public async Task SendReceive_ClassicStandardFrame()
    {
        var registry = CreateRegistry();
        // Classic CAN: fd=false 使用 V3 接口
        await using var bus = await registry.OpenAsync(
            "vector://virtual?deviceIndex=0&channelIndex=0&fd=false", new CanOpenOptions());

        using var sub = bus.Subscribe(new CanSubscriptionOptions { QueueCapacity = 64 });
        var frame = CanFrame.CreateData(CanId.Standard(0x123), [0x01, 0x02, 0x03]);

        var result = await bus.SendAsync(frame);
        Assert.AreEqual(CanTransmitSubmissionStatus.Accepted, result.Status);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await sub.ReadAsync(cts.Token);
        Assert.AreEqual(0x123u, received.Frame.Id.Value);
        Assert.AreEqual(3, received.Frame.Length);
    }

    [TestMethod]
    public async Task SendReceive_ClassicExtendedFrame()
    {
        var registry = CreateRegistry();
        // Classic CAN: fd=false 使用 V3 接口
        await using var bus = await registry.OpenAsync(
            "vector://virtual?deviceIndex=0&channelIndex=0&fd=false", new CanOpenOptions());

        using var sub = bus.Subscribe(new CanSubscriptionOptions { QueueCapacity = 64 });
        var frame = CanFrame.CreateData(CanId.Extended(0x1FFFFFFF), [0xAA]);

        await bus.SendAsync(frame);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await sub.ReadAsync(cts.Token);
        Assert.IsTrue(received.Frame.Id.IsExtended);
        Assert.AreEqual(0x1FFFFFFFu, received.Frame.Id.Value);
    }

    [TestMethod]
    public async Task SendReceive_FdStandardFrame()
    {
        var registry = CreateRegistry();
        await using var bus = await registry.OpenAsync(
            "vector://virtual?deviceIndex=0&channelIndex=0", new CanOpenOptions { BusParameters = CanBusParameters.Fd500k2M });

        using var sub = bus.Subscribe(new CanSubscriptionOptions { QueueCapacity = 64 });
        var payload = new byte[64];
        payload[0] = 0xBB;
        var frame = CanFrame.CreateFdData(CanId.Standard(0x300), payload);

        var result = await bus.SendAsync(frame);
        Assert.AreEqual(CanTransmitSubmissionStatus.Accepted, result.Status);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await sub.ReadAsync(cts.Token);
        Assert.AreEqual(0x300u, received.Frame.Id.Value);
        Assert.AreEqual(0xBB, received.Frame.GetPayloadByte(0));
    }

    [TestMethod]
    public async Task SendBatchAsync_MultipleFrames()
    {
        var registry = CreateRegistry();
        await using var bus = await registry.OpenAsync(
            "vector://virtual?deviceIndex=0&channelIndex=0", new CanOpenOptions());

        var frames = new[]
        {
            CanFrame.CreateData(CanId.Standard(0x100), [0x01]),
            CanFrame.CreateData(CanId.Standard(0x200), [0x02]),
            CanFrame.CreateData(CanId.Standard(0x300), [0x03])
        };

        var results = await bus.SendBatchAsync(frames);
        Assert.AreEqual(3, results.Length);
        foreach (var r in results)
            Assert.AreEqual(CanTransmitSubmissionStatus.Accepted, r.Status);
    }

    [TestMethod]
    public async Task CrossChannel_CH0ToCH1()
    {
        var registry = CreateRegistry();
        await using var bus0 = await registry.OpenAsync(
            "vector://virtual?deviceIndex=0&channelIndex=0", new CanOpenOptions());
        await using var bus1 = await registry.OpenAsync(
            "vector://virtual?deviceIndex=0&channelIndex=1", new CanOpenOptions());

        using var sub1 = bus1.Subscribe(new CanSubscriptionOptions { QueueCapacity = 64 });
        var frame = CanFrame.CreateData(CanId.Standard(0x400), [0x04, 0x05]);

        var r = await bus0.SendAsync(frame);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await sub1.ReadAsync(cts.Token);
        Assert.AreEqual(0x400u, received.Frame.Id.Value);
    }

    [TestMethod]
    public async Task Stress_HighVolumeSendReceive()
    {
        var registry = CreateRegistry();
        await using var bus = await registry.OpenAsync(
            "vector://virtual?deviceIndex=0&channelIndex=0", new CanOpenOptions());

        using var sub = bus.Subscribe(new CanSubscriptionOptions
        {
            QueueCapacity = 4096,
            FullMode = CanQueueFullMode.DropOldest
        });

        const int count = 100;
        for (int i = 0; i < count; i++)
        {
            var frame = CanFrame.CreateData(CanId.Standard((uint)(0x100 + i)), [(byte)i]);
            await bus.SendAsync(frame);
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = 0;
        try
        {
            while (received < count)
            {
                await sub.ReadAsync(cts.Token);
                received++;
            }
        }
        catch (OperationCanceledException) { }

        Assert.IsTrue(received > 0, $"Expected to receive at least some frames, got {received}");
    }

    [TestMethod]
    public async Task Error_InvalidDeviceType_Throws()
    {
        var registry = CreateRegistry();
        await Assert.ThrowsExactlyAsync<CanException>(
            () => registry.OpenAsync("vector://nonexistent?deviceIndex=0&channelIndex=0", new CanOpenOptions()).AsTask());
    }
}
