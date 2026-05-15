using CanHub.Adapter.Zlg.Tests.Support;

namespace CanHub.Adapter.Zlg.Tests.Hardware;

[TestClass]
[TestCategory("Hardware")]
public sealed class ZlgBus1TrafficHardwareTests : ZlgCanHubHardwareTestBase
{
    [TestMethod(DisplayName = "Bus1 ZLG0 CH0 to ZLG1 CH0 classic traffic works")]
    public async Task Bus1_Zlg0Ch0_ToZlg1Ch0_Classic500k()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        await using var tx = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);
        await using var rx = await OpenZlgAsync(registry, Env.Device1Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);
        using var rxSub = rx.Subscribe(new CanSubscriptionOptions());

        var frame = CanFrame.CreateData(CanId.Standard(0x321), [0x11, 0x22, 0x33, 0x44]);
        var submitted = await tx.SendAsync(frame, ct: TestContext.CancellationToken);
        TestContext.WriteLine($"ZLG classic SendAsync returned {submitted.Status}, native={submitted.NativeStatusCode}.");

        Assert.IsTrue(submitted.Accepted, "Bus1 has another ACK node open; ZLG transmit should be accepted.");
        var received = await WaitForFrameAsync(
            rxSub,
            candidate => candidate.Frame.Id.Value == frame.Id.Value && candidate.Frame.Kind == CanFrameKind.Data,
            TimeSpan.FromSeconds(2));

        CollectionAssert.AreEqual(CopyPayload(frame), CopyPayload(received.Frame));
    }

    [TestMethod(DisplayName = "Bus1 ZLG0 CH0 to ZLG1 CH0 CAN FD traffic works")]
    public async Task Bus1_Zlg0Ch0_ToZlg1Ch0_CanFd500k2M()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        await using var tx = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Fd500k2M, ct: TestContext.CancellationToken);
        await using var rx = await OpenZlgAsync(registry, Env.Device1Index, Env.Bus1Channel, CanBusParameters.Fd500k2M, ct: TestContext.CancellationToken);
        using var rxSub = rx.Subscribe(new CanSubscriptionOptions());

        var payload = Enumerable.Range(0, 20).Select(static i => (byte)(0x80 + i)).ToArray();
        var frame = CanFrame.CreateFdData(CanId.Standard(0x456), payload);
        var submitted = await tx.SendAsync(frame, ct: TestContext.CancellationToken);
        TestContext.WriteLine($"ZLG FD SendAsync returned {submitted.Status}, native={submitted.NativeStatusCode}.");

        Assert.IsTrue(submitted.Accepted, "Bus1 has another ACK node open; ZLG FD transmit should be accepted.");
        var received = await WaitForFrameAsync(
            rxSub,
            candidate => candidate.Frame.Id.Value == frame.Id.Value &&
                         candidate.Frame.Flags.HasFlag(CanFrameFlags.FD),
            TimeSpan.FromSeconds(2));

        CollectionAssert.AreEqual(payload, CopyPayload(received.Frame));
    }

    [TestMethod(DisplayName = "Bus1 ZLG0 CH0 to Vector VN5610A CH2 classic traffic works")]
    public async Task Bus1_Zlg0Ch0_ToVectorCh2_Classic500k()
    {
        RequireZlgHardware();
        RequireVectorHardware();

        var registry = CreateZlgVectorRegistry();
        await using var vector = await OpenVectorAsync(registry, CanBusParameters.Classic500k, TestContext.CancellationToken);
        await using var zlg = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Classic500k, ct: TestContext.CancellationToken);
        using var vectorSub = vector.Subscribe(new CanSubscriptionOptions());

        var frame = CanFrame.CreateData(CanId.Standard(0x471), [0x47, 0x01]);
        var submitted = await zlg.SendAsync(frame, ct: TestContext.CancellationToken);

        Assert.IsTrue(submitted.Accepted);
        var received = await WaitForFrameAsync(
            vectorSub,
            candidate => candidate.Frame.Id.Value == frame.Id.Value,
            TimeSpan.FromSeconds(2));
        CollectionAssert.AreEqual(CopyPayload(frame), CopyPayload(received.Frame));
    }

    [TestMethod(DisplayName = "Bus1 ZLG0 CH0 to Vector VN5610A CH2 CAN FD traffic works")]
    public async Task Bus1_Zlg0Ch0_ToVectorCh2_CanFd500k2M()
    {
        RequireZlgHardware();
        RequireVectorHardware();

        var registry = CreateZlgVectorRegistry();
        await using var vector = await OpenVectorAsync(registry, CanBusParameters.Fd500k2M, TestContext.CancellationToken);
        await using var zlg = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus1Channel, CanBusParameters.Fd500k2M, ct: TestContext.CancellationToken);
        using var vectorSub = vector.Subscribe(new CanSubscriptionOptions());

        var payload = Enumerable.Range(0, 24).Select(static i => (byte)(0x40 + i)).ToArray();
        var frame = CanFrame.CreateFdData(CanId.Standard(0x472), payload);
        var submitted = await zlg.SendAsync(frame, ct: TestContext.CancellationToken);

        Assert.IsTrue(submitted.Accepted);
        var received = await WaitForFrameAsync(
            vectorSub,
            candidate => candidate.Frame.Id.Value == frame.Id.Value &&
                         candidate.Frame.Flags.HasFlag(CanFrameFlags.FD),
            TimeSpan.FromSeconds(2));
        CollectionAssert.AreEqual(payload, CopyPayload(received.Frame));
    }
}
