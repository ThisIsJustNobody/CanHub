using CanHub.Adapter.Zlg.Tests.Support;

namespace CanHub.Adapter.Zlg.Tests.Hardware;

[TestClass]
[TestCategory("Hardware")]
public sealed class ZlgBus2TerminationHardwareTests : ZlgCanHubHardwareTestBase
{
    [TestMethod(DisplayName = "Bus2 termination disabled reports transmit failure or error frame")]
    public async Task Bus2_TerminationDisabled_ReportsSendFailureOrErrorFrame()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        var disabledClassic = new CanBusParameters
        {
            IsFd = false,
            ArbitrationBitrate = 500_000,
            TerminationEnabled = false,
        };
        await using var tx = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus2Channel, disabledClassic, ct: TestContext.CancellationToken);
        await using var ackOnly = await OpenZlgAsync(registry, Env.Device1Index, Env.Bus2Channel, disabledClassic, ct: TestContext.CancellationToken);
        using var txSub = tx.Subscribe(new CanSubscriptionOptions());

        var frame = CanFrame.CreateData(CanId.Standard(0x551), [0xDE, 0xAD]);
        var submitted = await tx.SendAsync(frame, ct: TestContext.CancellationToken);
        var errorFrame = await TryWaitForFrameAsync(
            txSub,
            candidate => candidate.Frame.Kind == CanFrameKind.Error ||
                         candidate.EventFlags.HasFlag(CanFrameEventFlags.ErrorResponse),
            TimeSpan.FromSeconds(2));
        TestContext.WriteLine($"Bus2 disabled SendAsync returned {submitted.Status}, native={submitted.NativeStatusCode}, errorObserved={errorFrame is not null}.");

        _ = ackOnly;
        Assert.IsTrue(
            !submitted.Accepted || errorFrame is not null,
            "Expected either a failed transmit submission or a ZLG merged error object when bus 2 has no termination.");
    }

    [TestMethod(DisplayName = "Bus2 termination enabled allows classic and CAN FD traffic")]
    public async Task Bus2_TerminationEnabled_AllowsClassicAndCanFdTraffic()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        var enabledFd = new CanBusParameters
        {
            IsFd = true,
            ArbitrationBitrate = 500_000,
            DataBitrate = 2_000_000,
            TerminationEnabled = true,
        };
        await using var tx = await OpenZlgAsync(registry, Env.Device0Index, Env.Bus2Channel, enabledFd, ct: TestContext.CancellationToken);
        await using var rx = await OpenZlgAsync(registry, Env.Device1Index, Env.Bus2Channel, enabledFd, ct: TestContext.CancellationToken);
        using var rxSub = rx.Subscribe(new CanSubscriptionOptions());

        var classic = CanFrame.CreateData(CanId.Standard(0x552), [0x01, 0x02, 0x03]);
        var classicSubmitted = await tx.SendAsync(classic, ct: TestContext.CancellationToken);
        Assert.IsTrue(classicSubmitted.Accepted);
        var classicReceived = await WaitForFrameAsync(
            rxSub,
            candidate => candidate.Frame.Id.Value == classic.Id.Value,
            TimeSpan.FromSeconds(2));
        CollectionAssert.AreEqual(CopyPayload(classic), CopyPayload(classicReceived.Frame));

        var fdPayload = Enumerable.Range(0, 32).Select(static i => (byte)i).ToArray();
        var fd = CanFrame.CreateFdData(CanId.Standard(0x553), fdPayload);
        var fdSubmitted = await tx.SendAsync(fd, ct: TestContext.CancellationToken);
        Assert.IsTrue(fdSubmitted.Accepted);
        var fdReceived = await WaitForFrameAsync(
            rxSub,
            candidate => candidate.Frame.Id.Value == fd.Id.Value &&
                         candidate.Frame.Flags.HasFlag(CanFrameFlags.FD),
            TimeSpan.FromSeconds(2));
        CollectionAssert.AreEqual(fdPayload, CopyPayload(fdReceived.Frame));
    }
}
