using System.Diagnostics;
using CanHub.Adapter.Vector;

namespace CanHub.Adapter.Zlg.Tests.Support;

public abstract class ZlgCanHubHardwareTestBase
{
    protected static ZlgCanHubEnvironment Env => ZlgCanHubEnvironment.FromEnvironment();

    public TestContext TestContext { get; set; } = null!;

    protected static void RequireZlgHardware()
    {
        if (!ZlgCanHubEnvironment.IsZlgHardwareEnabled)
            Assert.Inconclusive("Skipping ZLG hardware test: CANHUB_TEST_ZLG is not set.");
    }

    protected static void RequireVectorHardware()
    {
        if (!ZlgCanHubEnvironment.IsVectorHardwareEnabled)
            Assert.Inconclusive("Skipping Vector interop test: CANHUB_TEST_VECTOR is not set.");
    }

    protected static void RequireVectorBus2Hardware()
    {
        RequireVectorHardware();
        if (!ZlgCanHubEnvironment.IsVectorBus2HardwareEnabled)
            Assert.Inconclusive("Skipping Vector bus2 interop test: CANHUB_TEST_VECTOR_BUS2 is not set.");
    }

    protected static void RequireZlgOpenDiagnostics()
    {
        RequireZlgHardware();
        if (!ZlgCanHubEnvironment.IsZlgOpenDiagnosticsEnabled)
            Assert.Inconclusive("Skipping ZLG open diagnostics: CANHUB_TEST_ZLG_OPEN_DIAGNOSTICS is not set.");
    }

    protected static CanHubRegistry CreateZlgRegistry() =>
        CanHubRegistry.CreateDefault().AddZlgAdapter();

    protected static CanHubRegistry CreateZlgVectorRegistry() =>
        CanHubRegistry.CreateDefault().AddZlgAdapter().AddVectorAdapter();

    protected static ValueTask<ICanBus> OpenZlgAsync(
        CanHubRegistry registry,
        uint deviceIndex,
        uint channelIndex,
        CanBusParameters busParameters,
        ZlgOpenOptions? nativeOptions = null,
        CancellationToken ct = default,
        CanRecoveryOptions? recovery = null) =>
        registry.OpenAsync(
            $"zlg://USBCANFD_200U?deviceIndex={deviceIndex}&channel={channelIndex}",
            new CanOpenOptions
            {
                BusParameters = busParameters,
                NativeOptions = nativeOptions,
                Recovery = recovery ?? CanRecoveryOptions.Disabled,
            },
            ct);

    protected static ValueTask<ICanBus> OpenVectorAsync(
        CanHubRegistry registry,
        CanBusParameters busParameters,
        CancellationToken ct = default,
        CanRecoveryOptions? recovery = null) =>
        registry.OpenAsync(
            $"vector://{Env.VectorDeviceName}?deviceIndex={Env.VectorDeviceIndex}&channel={Env.VectorChannelIndex}",
            new CanOpenOptions
            {
                BusParameters = busParameters,
                Recovery = recovery ?? CanRecoveryOptions.Disabled,
            },
            ct);

    protected static async Task<CanFrameEvent> WaitForFrameAsync(
        ICanSubscription subscription,
        Predicate<CanFrameEvent> predicate,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            while (true)
            {
                var frame = await subscription.ReadAsync(cts.Token).ConfigureAwait(false);
                if (predicate(frame))
                    return frame;
            }
        }
        catch (OperationCanceledException)
        {
            Assert.Fail($"Timed out waiting for CAN frame after {timeout}.");
            throw new UnreachableException();
        }
    }

    protected static async Task<CanFrameEvent?> TryWaitForFrameAsync(
        ICanSubscription subscription,
        Predicate<CanFrameEvent> predicate,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            while (true)
            {
                var frame = await subscription.ReadAsync(cts.Token).ConfigureAwait(false);
                if (predicate(frame))
                    return frame;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    protected static byte[] CopyPayload(CanFrame frame)
    {
        var payload = new byte[frame.Length];
        frame.CopyPayloadTo(payload);
        return payload;
    }
}

public sealed record ZlgCanHubEnvironment(
    uint Device0Index,
    uint Device1Index,
    uint Bus1Channel,
    uint Bus2Channel,
    uint ScanDepth,
    string VectorDeviceName,
    uint VectorDeviceIndex,
    uint VectorChannelIndex)
{
    public static bool IsZlgHardwareEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CANHUB_TEST_ZLG"));

    public static bool IsVectorHardwareEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CANHUB_TEST_VECTOR"));

    public static bool IsVectorBus2HardwareEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CANHUB_TEST_VECTOR_BUS2"));

    public static bool IsZlgOpenDiagnosticsEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CANHUB_TEST_ZLG_OPEN_DIAGNOSTICS"));

    public static ZlgCanHubEnvironment FromEnvironment() =>
        new(
            GetUInt32("CANHUB_TEST_ZLG_DEVICE0", 0),
            GetUInt32("CANHUB_TEST_ZLG_DEVICE1", 1),
            GetUInt32("CANHUB_TEST_ZLG_BUS1_CHANNEL", 0),
            GetUInt32("CANHUB_TEST_ZLG_BUS2_CHANNEL", 1),
            GetUInt32("CANHUB_TEST_ZLG_SCAN_DEPTH", 2),
            GetString("CANHUB_TEST_VECTOR_DEVICE", "VN5610A"),
            GetUInt32("CANHUB_TEST_VECTOR_DEVICE_INDEX", GetUInt32("CANHUB_TEST_VECTOR_DEVICE", 0)),
            GetUInt32("CANHUB_TEST_VECTOR_CHANNEL_INDEX", 2));

    private static string GetString(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) || uint.TryParse(value, out _) ? defaultValue : value;
    }

    private static uint GetUInt32(string name, uint defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return uint.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
