using System.Collections.Concurrent;
using System.Diagnostics;
using CanHub.Adapter.Zlg.Tests.Support;

namespace CanHub.Adapter.Zlg.Tests.Hardware;

[TestClass]
[TestCategory("Hardware")]
public sealed class ZlgBus2TerminationHardwareTests : ZlgCanHubHardwareTestBase
{
    private static readonly CanRecoveryTrigger HardwareFaultTriggers =
        CanRecoveryTrigger.BusOff |
        CanRecoveryTrigger.ErrorPassive |
        CanRecoveryTrigger.NativeReceiveFault;

    [TestMethod(DisplayName = "Bus1 terminated single node triggers No ACK recovery and then talks to peer")]
    public async Task Bus1_SingleNodeNoAck_ResetRecovery_ReopensAndThenTalksToPeer()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        await using var tx = await OpenZlgAsync(
            registry,
            Env.Device0Index,
            Env.Bus1Channel,
            CanBusParameters.Classic500k,
            recovery: CanRecoveryOptions.ResetOnFault(
                triggers: HardwareFaultTriggers,
                restartDelay: TimeSpan.Zero),
            ct: TestContext.CancellationToken);
        tx.StatusChanged += statuses.Enqueue;

        var noAckFrame = CanFrame.CreateData(CanId.Standard(0x571), [0x57, 0x10]);
        var submitted = await tx.SendAsync(noAckFrame, ct: TestContext.CancellationToken);
        TestContext.WriteLine($"Bus1 single-node send returned {submitted.Status}, native={submitted.NativeStatusCode}.");

        var fault = await WaitForStatusAsync(
            statuses,
            status => status.Code is CanStatusCode.NativeDriverError or CanStatusCode.BusOff,
            TimeSpan.FromSeconds(5));
        var recovered = await WaitForStatusAsync(
            statuses,
            status => status.Code == CanStatusCode.Recovered,
            TimeSpan.FromSeconds(5));
        TestContext.WriteLine($"Bus1 single-node fault={fault.Code}/{fault.NativeErrorCode:X8}, recovered attempts={recovered.Count}.");

        await using var peer = await OpenZlgAsync(
            registry,
            Env.Device1Index,
            Env.Bus1Channel,
            CanBusParameters.Classic500k,
            ct: TestContext.CancellationToken);
        using var peerSub = peer.Subscribe(new CanSubscriptionOptions());

        var postRecoveryFrame = CanFrame.CreateData(CanId.Standard(0x572), [0x57, 0x20]);
        var postRecoverySubmitted = await tx.SendAsync(postRecoveryFrame, ct: TestContext.CancellationToken);
        Assert.IsTrue(postRecoverySubmitted.Accepted, "Recovered ZLG channel should accept a transmit once a peer is online.");
        var received = await WaitForFrameAsync(
            peerSub,
            candidate => candidate.Frame.Id.Value == postRecoveryFrame.Id.Value,
            TimeSpan.FromSeconds(2));
        CollectionAssert.AreEqual(CopyPayload(postRecoveryFrame), CopyPayload(received.Frame));
    }

    [TestMethod(DisplayName = "Bus2 unterminated native bus error triggers recovery attempt")]
    public async Task Bus2_UnterminatedNativeBusError_TriggersRecoveryAttempt()
    {
        RequireZlgHardware();

        var registry = CreateZlgRegistry();
        var disabledClassic = new CanBusParameters
        {
            IsFd = false,
            ArbitrationBitrate = 500_000,
            TerminationEnabled = false,
        };
        var statuses = new ConcurrentQueue<CanStatusEvent>();
        await using var tx = await OpenZlgAsync(
            registry,
            Env.Device0Index,
            Env.Bus2Channel,
            disabledClassic,
            recovery: CanRecoveryOptions.ReopenWithBackoff(
                triggers: HardwareFaultTriggers,
                restartDelay: TimeSpan.Zero,
                maxAttempts: 2,
                maxBackoffDelay: TimeSpan.Zero),
            ct: TestContext.CancellationToken);
        await using var ackOnly = await OpenZlgAsync(
            registry,
            Env.Device1Index,
            Env.Bus2Channel,
            disabledClassic,
            ct: TestContext.CancellationToken);
        tx.StatusChanged += statuses.Enqueue;

        var frame = CanFrame.CreateData(CanId.Standard(0x573), [0x57, 0x30]);
        var submitted = await tx.SendAsync(frame, ct: TestContext.CancellationToken);
        TestContext.WriteLine($"Bus2 unterminated send returned {submitted.Status}, native={submitted.NativeStatusCode}.");

        var fault = await WaitForStatusAsync(
            statuses,
            status => status.Code is CanStatusCode.NativeDriverError or CanStatusCode.BusOff,
            TimeSpan.FromSeconds(5));
        var recovered = await WaitForStatusAsync(
            statuses,
            status => status.Code == CanStatusCode.Recovered,
            TimeSpan.FromSeconds(5));
        TestContext.WriteLine($"Bus2 unterminated fault={fault.Code}/{fault.NativeErrorCode:X8}, recovered attempts={recovered.Count}.");

        _ = ackOnly;
        Assert.IsTrue(recovered.Count is >= 1 and <= 2);
    }

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

    private static async Task<CanStatusEvent> WaitForStatusAsync(
        ConcurrentQueue<CanStatusEvent> statuses,
        Predicate<CanStatusEvent> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            foreach (var status in statuses)
            {
                if (predicate(status))
                    return status;
            }

            await Task.Delay(20);
        }

        Assert.Fail($"Timed out waiting for ZLG status. Observed: {string.Join(", ", statuses.Select(static s => s.Code))}");
        throw new UnreachableException();
    }
}
