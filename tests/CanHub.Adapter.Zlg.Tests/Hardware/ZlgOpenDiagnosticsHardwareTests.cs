using CanHub.Adapter.Zlg.Tests.Support;

namespace CanHub.Adapter.Zlg.Tests.Hardware;

/// <summary>
/// ZLG 打开顺序诊断用例。
/// 该用例只打开/关闭通道，不发送报文，用来把问题限定在 ZCAN_OpenDevice/OpenCAN/StartCAN 这一类生命周期阶段。
/// </summary>
[TestClass]
[TestCategory("Hardware")]
[TestCategory("Diagnostics")]
public sealed class ZlgOpenDiagnosticsHardwareTests : ZlgCanHubHardwareTestBase
{
    private const int DefaultIterations = 3;

    [TestMethod(DisplayName = "Bus1 two ZLG devices open order diagnostics")]
    public async Task Bus1_TwoZlgDevices_OpenOrderDiagnostics()
    {
        RequireZlgOpenDiagnostics();

        var iterations = GetIterations();
        var timing = GetTiming();
        TestContext.WriteLine("ZLG open diagnostics only opens and closes channels; it does not transmit frames.");
        TestContext.WriteLine(
            $"Topology under test: device0={Env.Device0Index}, device1={Env.Device1Index}, bus1Channel={Env.Bus1Channel}, parameters={FormatParameters(CanBusParameters.Classic500k)}, iterations={iterations}, interOpenDelay={timing.InterOpenDelay.TotalMilliseconds}ms, afterCloseDelay={timing.AfterCloseDelay.TotalMilliseconds}ms.");

        // 不在第一个失败处立即中止：这样一次运行可以看到单设备打开、正序打开、反序打开各自是否失败。
        var failures = new List<string>();

        // 先分别单独打开两个设备，排除“必须同时打开两个节点才失败”的干扰。
        await RunSingleOpenAsync("single-open device0/bus1", Env.Device0Index, Env.Bus1Channel, timing, failures);
        await RunSingleOpenAsync("single-open device1/bus1", Env.Device1Index, Env.Bus1Channel, timing, failures);

        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            // 正序：先打开设备 0，再在设备 0 保持打开时打开设备 1。
            await RunOpenPairAsync(
                $"pair-open iteration {iteration}: device0 then device1",
                firstDeviceIndex: Env.Device0Index,
                secondDeviceIndex: Env.Device1Index,
                channelIndex: Env.Bus1Channel,
                timing,
                failures);

            // 反序：先打开设备 1，再在设备 1 保持打开时打开设备 0，用来判断是否和特定设备或打开顺序有关。
            await RunOpenPairAsync(
                $"pair-open iteration {iteration}: device1 then device0",
                firstDeviceIndex: Env.Device1Index,
                secondDeviceIndex: Env.Device0Index,
                channelIndex: Env.Bus1Channel,
                timing,
                failures);
        }

        if (failures.Count > 0)
            Assert.Fail(
                $"ZLG open diagnostics found {failures.Count} failure(s). See TestContext output for full exception details.{Environment.NewLine}" +
                string.Join(Environment.NewLine, failures));
    }

    private async Task RunSingleOpenAsync(
        string step,
        uint deviceIndex,
        uint channelIndex,
        DiagnosticTiming timing,
        List<string> failures)
    {
        var endpoint = GetZlgEndpoint(deviceIndex, channelIndex);
        TestContext.WriteLine($"[BEGIN] {step}: opening {endpoint}.");
        ICanBus? bus = null;
        Exception? openException = null;

        try
        {
            var registry = CreateZlgRegistry();
            bus = await OpenZlgAsync(
                registry,
                deviceIndex,
                channelIndex,
                CanBusParameters.Classic500k,
                ct: TestContext.CancellationToken);

            TestContext.WriteLine($"[OPEN] {step}: displayName='{bus.DisplayName}', isOpen={bus.IsOpen}.");
        }
        catch (Exception ex)
        {
            openException = ex;
        }
        finally
        {
            if (bus is not null)
                await DisposeBusAsync(step, "single", endpoint, bus, timing);
        }

        if (openException is not null)
        {
            RecordOpenFailure(step, endpoint, openException, failures);
            return;
        }

        TestContext.WriteLine($"[END] {step}: closed {endpoint}.");
    }

    private async Task RunOpenPairAsync(
        string step,
        uint firstDeviceIndex,
        uint secondDeviceIndex,
        uint channelIndex,
        DiagnosticTiming timing,
        List<string> failures)
    {
        var firstEndpoint = GetZlgEndpoint(firstDeviceIndex, channelIndex);
        var secondEndpoint = GetZlgEndpoint(secondDeviceIndex, channelIndex);
        TestContext.WriteLine($"[BEGIN] {step}: first={firstEndpoint}, second={secondEndpoint}.");

        ICanBus? first = null;
        ICanBus? second = null;
        Exception? openException = null;
        string? failedEndpoint = null;

        // 打开失败先记录下来，随后仍进入 finally 清理已打开的通道。
        // 这样清理异常不会覆盖真正的 StartCAN/OpenCAN 失败位置。
        try
        {
            var registry = CreateZlgRegistry();

            TestContext.WriteLine($"[OPEN-FIRST] {step}: opening {firstEndpoint}.");
            first = await OpenZlgAsync(
                registry,
                firstDeviceIndex,
                channelIndex,
                CanBusParameters.Classic500k,
                ct: TestContext.CancellationToken);
            TestContext.WriteLine($"[OPEN-FIRST-DONE] {step}: displayName='{first.DisplayName}', isOpen={first.IsOpen}.");

            // 这个等待用于模拟调试模式或官方工具里自然存在的操作间隔。
            await DelayIfNeededAsync(timing.InterOpenDelay, $"[DELAY-INTER-OPEN] {step}: waiting before opening second channel.");

            TestContext.WriteLine($"[OPEN-SECOND] {step}: opening {secondEndpoint} while first is still open.");
            second = await OpenZlgAsync(
                registry,
                secondDeviceIndex,
                channelIndex,
                CanBusParameters.Classic500k,
                ct: TestContext.CancellationToken);
            TestContext.WriteLine($"[OPEN-SECOND-DONE] {step}: displayName='{second.DisplayName}', isOpen={second.IsOpen}.");
        }
        catch (Exception ex)
        {
            openException = ex;
            failedEndpoint = first is null ? firstEndpoint : secondEndpoint;
        }
        finally
        {
            if (second is not null)
            {
                await DisposeBusAsync(step, "second", secondEndpoint, second, timing);
            }

            if (first is not null)
            {
                await DisposeBusAsync(step, "first", firstEndpoint, first, timing);
            }
        }

        if (openException is not null)
        {
            RecordOpenFailure(step, failedEndpoint ?? "<unknown>", openException, failures);
            return;
        }

        TestContext.WriteLine($"[END] {step}: both channels opened and closed successfully.");
    }

    /// <summary>
    /// 关闭清理只写入诊断日志，不作为本用例的主失败原因。
    /// 本用例关注的是打开阶段是否失败。
    /// </summary>
    private async Task DisposeBusAsync(string step, string slot, string endpoint, ICanBus bus, DiagnosticTiming timing)
    {
        try
        {
            await bus.DisposeAsync().ConfigureAwait(false);
            TestContext.WriteLine($"[CLOSE-{slot.ToUpperInvariant()}] {step}: closed {endpoint}.");
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"[CLOSE-{slot.ToUpperInvariant()}-FAILED] {step}: failed to close {endpoint}.{Environment.NewLine}{ex}");
        }

        // 这个等待用于验证 ZCAN_ResetCAN/ZCAN_CloseDevice 返回后，驱动内部是否仍需要 settle time。
        await DelayIfNeededAsync(timing.AfterCloseDelay, $"[DELAY-AFTER-CLOSE] {step}: waiting after closing {endpoint}.");
    }

    private static string GetZlgEndpoint(uint deviceIndex, uint channelIndex) =>
        $"zlg://USBCANFD_200U?deviceIndex={deviceIndex}&channel={channelIndex}";

    private static int GetIterations()
    {
        var value = Environment.GetEnvironmentVariable("CANHUB_TEST_ZLG_OPEN_DIAG_ITERATIONS");
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : DefaultIterations;
    }

    private static DiagnosticTiming GetTiming()
    {
        var stepDelay = GetDelay("CANHUB_TEST_ZLG_OPEN_DIAG_STEP_DELAY_MS", TimeSpan.Zero);
        return new DiagnosticTiming(
            GetDelay("CANHUB_TEST_ZLG_OPEN_DIAG_INTER_OPEN_DELAY_MS", stepDelay),
            GetDelay("CANHUB_TEST_ZLG_OPEN_DIAG_AFTER_CLOSE_DELAY_MS", stepDelay));
    }

    private static TimeSpan GetDelay(string environmentVariableName, TimeSpan defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);
        return int.TryParse(value, out var milliseconds) && milliseconds > 0
            ? TimeSpan.FromMilliseconds(milliseconds)
            : defaultValue;
    }

    private async Task DelayIfNeededAsync(TimeSpan delay, string message)
    {
        if (delay <= TimeSpan.Zero)
            return;

        TestContext.WriteLine($"{message} delay={delay.TotalMilliseconds}ms.");
        await Task.Delay(delay, TestContext.CancellationToken).ConfigureAwait(false);
    }

    // 直接打印所有会影响打开配置的关键参数，方便和官方工具配置逐项对照。
    private static string FormatParameters(CanBusParameters parameters) =>
        $"isFd={parameters.IsFd}, arbitration={parameters.ArbitrationBitrate}, data={parameters.DataBitrate?.ToString() ?? "<null>"}, termination={parameters.TerminationEnabled?.ToString() ?? "<null>"}";

    /// <summary>
    /// 记录失败摘要，同时把完整异常写入 TestContext，便于从测试输出复制堆栈。
    /// </summary>
    private void RecordOpenFailure(string step, string endpoint, Exception exception, List<string> failures)
    {
        failures.Add($"- {step}: {endpoint} -> {exception.GetType().Name}: {exception.Message}");
        TestContext.WriteLine(
            $"[FAILED] {step}: endpoint={endpoint}.{Environment.NewLine}" +
            FormatException(exception));
    }

    private static string FormatException(Exception exception)
    {
        if (exception is not CanException canException)
            return exception.ToString();

        return
            $"CanException: adapter={canException.AdapterId}, category={canException.Category}, endpoint={canException.Endpoint?.ToString() ?? "<none>"}, " +
            $"nativeFunction={canException.NativeFunction ?? "<none>"}, vendorCode={canException.VendorCode?.ToString() ?? "<none>"}, recoverability={canException.Recoverability}.{Environment.NewLine}" +
            exception;
    }

    private sealed record DiagnosticTiming(TimeSpan InterOpenDelay, TimeSpan AfterCloseDelay);
}
