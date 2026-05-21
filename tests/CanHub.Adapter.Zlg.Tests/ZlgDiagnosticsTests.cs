using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg.Tests;

[TestClass]
public sealed class ZlgDiagnosticsTests
{
    [TestMethod(DisplayName = "ZLG diagnostics on non-Windows skips native probing and scan")]
    public async Task CheckRuntimeAsync_NonWindows_SkipsNativeAndScan()
    {
        var probe = new FakeProbe { IsWindows = false };

        var report = await ZlgDiagnostics.CheckRuntimeAsync(probe, ct: TestContext.CancellationToken);

        Assert.IsFalse(report.IsWindows);
        Assert.AreEqual("x64", report.ProcessArchitecture);
        Assert.IsFalse(report.NativeLibraryFound);
        Assert.IsFalse(report.NativeLibraryLoaded);
        Assert.IsFalse(report.ScanSucceeded);
        Assert.IsFalse(report.HasOpenableChannel);
        Assert.IsFalse(report.IsReady);
        Assert.AreEqual(0, probe.FindNativeLibraryCallCount);
        Assert.AreEqual(0, probe.LoadNativeLibraryCallCount);
        Assert.AreEqual(0, probe.ScanCallCount);
        Assert.HasCount(1, report.Diagnostics);
        Assert.AreEqual("zlg", report.Diagnostics[0].AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, report.Diagnostics[0].Category);
    }

    [TestMethod(DisplayName = "ZLG diagnostics reports missing native runtime")]
    public async Task CheckRuntimeAsync_NativeMissing_ReturnsDiagnostic()
    {
        var probe = new FakeProbe
        {
            FindNativeLibraryException = new DllNotFoundException("zlgcan.dll missing"),
        };

        var report = await ZlgDiagnostics.CheckRuntimeAsync(probe, ct: TestContext.CancellationToken);

        Assert.IsTrue(report.IsWindows);
        Assert.IsFalse(report.NativeLibraryFound);
        Assert.IsFalse(report.NativeLibraryLoaded);
        Assert.IsFalse(report.ScanSucceeded);
        Assert.IsFalse(report.IsReady);
        Assert.IsNull(report.NativeLibraryPath);
        Assert.AreEqual(1, probe.FindNativeLibraryCallCount);
        Assert.AreEqual(0, probe.LoadNativeLibraryCallCount);
        Assert.AreEqual(0, probe.ScanCallCount);
        Assert.HasCount(1, report.Diagnostics);
        Assert.AreEqual(CanErrorCategory.AdapterError, report.Diagnostics[0].Category);
        Assert.IsFalse(string.IsNullOrWhiteSpace(report.Diagnostics[0].Hint));
    }

    [TestMethod(DisplayName = "ZLG diagnostics marks ready when scan finds openable channel")]
    public async Task CheckRuntimeAsync_OpenableChannel_MarksReady()
    {
        var channel = new CanChannelInfo(
            adapterId: "zlg",
            deviceName: "USBCANFD_200U",
            deviceIndex: 0,
            channelIndex: 0,
            nativeChannelIndex: 0,
            endpoint: "zlg://USBCANFD_200U?deviceIndex=0&channelIndex=0",
            availability: CanChannelAvailability.Available);
        var probe = new FakeProbe
        {
            NativeLibraryPath = "C:\\canhub\\zlg\\x64\\zlgcan.dll",
            ScanResult = new CanChannelScanResult([channel]),
        };

        var report = await ZlgDiagnostics.CheckRuntimeAsync(probe, ct: TestContext.CancellationToken);

        Assert.IsTrue(report.NativeLibraryFound);
        Assert.IsTrue(report.NativeLibraryLoaded);
        Assert.IsTrue(report.ScanSucceeded);
        Assert.IsTrue(report.HasOpenableChannel);
        Assert.IsTrue(report.IsReady);
        Assert.AreEqual("C:\\canhub\\zlg\\x64\\zlgcan.dll", report.NativeLibraryPath);
        Assert.HasCount(1, report.Channels);
        Assert.AreEqual("zlg://USBCANFD_200U?channelIndex=0&deviceIndex=0", report.Channels[0].CanonicalEndpoint);
        Assert.IsEmpty(report.Diagnostics);
    }

    [TestMethod(DisplayName = "ZLG diagnostics scan without channels is not ready")]
    public async Task CheckRuntimeAsync_ScanWithoutChannels_IsNotReady()
    {
        var probe = new FakeProbe
        {
            NativeLibraryPath = "C:\\canhub\\zlg\\x64\\zlgcan.dll",
            ScanResult = new CanChannelScanResult(),
        };

        var report = await ZlgDiagnostics.CheckRuntimeAsync(probe, ct: TestContext.CancellationToken);

        Assert.IsTrue(report.NativeLibraryFound);
        Assert.IsTrue(report.NativeLibraryLoaded);
        Assert.IsTrue(report.ScanSucceeded);
        Assert.IsFalse(report.HasOpenableChannel);
        Assert.IsFalse(report.IsReady);
        Assert.IsEmpty(report.Channels);
    }

    [TestMethod(DisplayName = "ZLG diagnostics preserves scan diagnostics")]
    public async Task CheckRuntimeAsync_ScanDiagnostics_ArePreserved()
    {
        var diagnostic = new ScanDiagnostic(
            CanErrorCategory.AdapterError,
            "scan warning",
            adapterId: "zlg",
            hint: "driver check");
        var probe = new FakeProbe
        {
            NativeLibraryPath = "C:\\canhub\\zlg\\x64\\zlgcan.dll",
            ScanResult = new CanChannelScanResult(diagnostics: [diagnostic]),
        };

        var report = await ZlgDiagnostics.CheckRuntimeAsync(probe, ct: TestContext.CancellationToken);

        Assert.IsTrue(report.ScanSucceeded);
        Assert.IsFalse(report.IsReady);
        Assert.HasCount(1, report.Diagnostics);
        Assert.AreSame(diagnostic, report.Diagnostics[0]);
    }

    private sealed class FakeProbe : IZlgRuntimeDiagnosticProbe
    {
        public bool IsWindows { get; init; } = true;
        public string ProcessArchitecture { get; init; } = "x64";
        public string? NativeLibraryPath { get; init; }
        public Exception? FindNativeLibraryException { get; init; }
        public CanChannelScanResult ScanResult { get; init; } = new();

        public int FindNativeLibraryCallCount { get; private set; }
        public int LoadNativeLibraryCallCount { get; private set; }
        public int ScanCallCount { get; private set; }

        public string FindNativeLibrary()
        {
            FindNativeLibraryCallCount++;
            if (FindNativeLibraryException is not null)
                throw FindNativeLibraryException;

            return NativeLibraryPath ?? "C:\\canhub\\zlg\\x64\\zlgcan.dll";
        }

        public void LoadNativeLibrary()
        {
            LoadNativeLibraryCallCount++;
        }

        public ValueTask<CanChannelScanResult> ScanAsync(
            ScanOptions? options,
            CancellationToken ct = default)
        {
            ScanCallCount++;
            return ValueTask.FromResult(ScanResult);
        }
    }

    public TestContext TestContext { get; set; }
}
