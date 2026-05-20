using CanHub;

namespace CanHub.Core.Tests;

[TestClass]
public sealed class CanHubRegistryScanTests
{
    [TestMethod]
    public async Task ScanAsync_NoAdapters_ReturnsEmpty()
    {
        var registry = CanHubRegistry.CreateDefault();

        var result = await registry.ScanAsync();

        Assert.IsEmpty(result.Channels);
        Assert.IsEmpty(result.Diagnostics);
    }

    [TestMethod]
    public async Task ScanAsync_SkipsAdaptersWithChannelScanNotSupported()
    {
        var registry = CanHubRegistry.CreateDefault();
        registry.AddAdapter(new FakeAdapterProvider("virtual", "Virtual", new[] { "virtual" },
            supportsScan: false));

        var result = await registry.ScanAsync();

        Assert.IsEmpty(result.Channels);
        Assert.IsEmpty(result.Diagnostics);
    }

    [TestMethod]
    public async Task ScanAsync_CallsAdaptersWithScanSupported()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("zlg", "ZLG", new[] { "zlg" },
            supportsScan: true,
            scanResult: new CanChannelScanResult([
                new CanChannelInfo("zlg", "USBCANFD-200U", 0, 1, null, "zlg://USBCANFD-200U?deviceIndex=0&channelIndex=1",
                    CanChannelAvailability.Available)
            ]));
        registry.AddAdapter(provider);

        var result = await registry.ScanAsync();

        Assert.HasCount(1, result.Channels);
        Assert.AreEqual("zlg", result.Channels[0].AdapterId);
        Assert.AreEqual("zlg://USBCANFD-200U?deviceIndex=0&channelIndex=1", result.Channels[0].Endpoint);
        Assert.IsTrue(provider.ScanCalled);
    }

    [TestMethod]
    public async Task ScanAsync_WithFilter_FiltersCorrectly()
    {
        var registry = CanHubRegistry.CreateDefault();
        registry.AddAdapter(new FakeAdapterProvider("zlg", "ZLG", new[] { "zlg" },
            supportsScan: true,
            scanResult: new CanChannelScanResult([
                new CanChannelInfo("zlg", "ZLG", 0, 0, null, "zlg://ZLG?deviceIndex=0&channelIndex=0",
                    CanChannelAvailability.Available)
            ])));
        registry.AddAdapter(new FakeAdapterProvider("vector", "Vector", new[] { "vector" },
            supportsScan: true,
            scanResult: new CanChannelScanResult([
                new CanChannelInfo("vector", "VN1610", 0, 0, null, "vector://VN1610?deviceIndex=0&channelIndex=0",
                    CanChannelAvailability.Available)
            ])));

        var result = await registry.ScanAsync(p => p.AdapterId == "zlg");

        Assert.HasCount(1, result.Channels);
        Assert.AreEqual("zlg", result.Channels[0].AdapterId);
    }

    [TestMethod]
    public async Task ScanAsync_AdapterThrowsReturnsDiagnosticAndContinuesOtherAdapters()
    {
        var registry = CanHubRegistry.CreateDefault();
        registry.AddAdapter(new FakeAdapterProvider("fail", "Fail", new[] { "fail" },
            supportsScan: true,
            throwOnScan: true));
        registry.AddAdapter(new FakeAdapterProvider("ok", "OK", new[] { "ok" },
            supportsScan: true,
            scanResult: new CanChannelScanResult([
                new CanChannelInfo("ok", "OK", 0, 0, null, "ok://OK?deviceIndex=0&channelIndex=0",
                    CanChannelAvailability.Available)
            ])));

        var result = await registry.ScanAsync();

        Assert.HasCount(1, result.Channels);
        Assert.AreEqual("ok", result.Channels[0].AdapterId);
        Assert.HasCount(1, result.Diagnostics);
        Assert.AreEqual("fail", result.Diagnostics[0].AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, result.Diagnostics[0].Category);
        StringAssert.Contains(result.Diagnostics[0].Message, nameof(InvalidOperationException));
        StringAssert.Contains(result.Diagnostics[0].Message, "Scan failed");
    }

    [TestMethod]
    public async Task ScanAsync_CanExceptionDiagnosticCarriesHintAndDetails()
    {
        var registry = CanHubRegistry.CreateDefault();
        var scanException = new CanException(
            "zlg",
            CanErrorCategory.AdapterError,
            "ZLG native driver is unavailable.",
            endpoint: CanEndpoint.Parse("zlg://USBCANFD_200U?deviceIndex=0&channelIndex=0"),
            nativeFunction: "ZCAN_OpenDevice",
            vendorCode: 0,
            recoverability: CanRecoverability.Retryable,
            hint: "确认 ZLG 驱动已安装，且 native DLL 可被当前进程加载。",
            details: new Dictionary<string, string>
            {
                ["deviceIndex"] = "0",
                ["channelIndex"] = "0",
                ["nativeDllPath"] = "<native-dll-path>",
            });
        registry.AddAdapter(new FakeAdapterProvider("zlg", "ZLG", new[] { "zlg" },
            supportsScan: true,
            scanException: scanException));

        var result = await registry.ScanAsync();

        Assert.IsEmpty(result.Channels);
        Assert.HasCount(1, result.Diagnostics);
        var diagnostic = result.Diagnostics[0];
        Assert.AreEqual("zlg", diagnostic.AdapterId);
        Assert.AreEqual(CanErrorCategory.AdapterError, diagnostic.Category);
        Assert.AreEqual("zlg://USBCANFD_200U?channelIndex=0&deviceIndex=0", diagnostic.Endpoint);
        Assert.AreEqual("确认 ZLG 驱动已安装，且 native DLL 可被当前进程加载。", diagnostic.Hint);
        Assert.AreEqual("0", diagnostic.Details["deviceIndex"]);
        Assert.AreEqual("0", diagnostic.Details["channelIndex"]);
        Assert.AreEqual("<native-dll-path>", diagnostic.Details["nativeDllPath"]);
    }

    [TestMethod]
    public async Task ScanAsync_CancellationPropagates()
    {
        var registry = CanHubRegistry.CreateDefault();
        registry.AddAdapter(new FakeAdapterProvider("cancel", "Cancel", new[] { "cancel" },
            supportsScan: true,
            throwOnCancellation: true));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => registry.ScanAsync(options: null, ct: cts.Token).AsTask());
    }

    [TestMethod]
    public async Task OpenAsync_ChannelInfoUsesEndpoint()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("vector", "Vector", new[] { "vector" });
        registry.AddAdapter(provider);
        var channel = new CanChannelInfo("vector", "VN5610A", 0, 2, 5,
            "vector://VN5610A?deviceIndex=0&channelIndex=2", CanChannelAvailability.Active);

        await using var bus = await registry.OpenAsync(channel, new CanOpenOptions(), CancellationToken.None);

        Assert.AreEqual("Vector", bus.DisplayName);
        Assert.IsNotNull(provider.LastOpenContext);
        Assert.AreEqual("VN5610A", provider.LastOpenContext.Endpoint.Device);
        Assert.AreEqual(2, provider.LastOpenContext.Endpoint.ChannelIndex);
        Assert.AreEqual(2, provider.LastOpenContext.Endpoint.Channel);
    }

    [TestMethod]
    public async Task OpenAsync_UnsupportedChannelThrows()
    {
        var registry = CanHubRegistry.CreateDefault();
        var channel = new CanChannelInfo("vector", "VN5610A", 0, 0, 0,
            null, CanChannelAvailability.Unsupported);

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            () => registry.OpenAsync(channel, new CanOpenOptions(), CancellationToken.None).AsTask());

        Assert.AreEqual(CanErrorCategory.InvalidEndpoint, ex.Category);
    }

    [TestMethod]
    public async Task OpenAsync_UnsupportedChannelCarriesScanDiagnostic()
    {
        var registry = CanHubRegistry.CreateDefault();
        var diagnostic = new ScanDiagnostic(
            CanErrorCategory.AdapterError,
            "Vector channel is not CAN-compatible.",
            nativeErrorCode: 42,
            recoverability: CanRecoverability.Retryable,
            adapterId: "vector",
            endpoint: "vector://VN5610A?deviceIndex=0&channelIndex=2",
            hint: "请选择支持 CAN 的 Vector 通道。",
            details: new Dictionary<string, string>
            {
                ["deviceIndex"] = "0",
                ["channelIndex"] = "2",
            });
        var channel = new CanChannelInfo(
            "vector",
            "VN5610A",
            0,
            2,
            5,
            null,
            CanChannelAvailability.Unsupported,
            diagnostic: diagnostic);

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            () => registry.OpenAsync(channel, new CanOpenOptions(), CancellationToken.None).AsTask());

        Assert.AreEqual(CanErrorCategory.AdapterError, ex.Category);
        Assert.AreEqual("Vector channel is not CAN-compatible.", ex.Message);
        Assert.AreEqual("vector://VN5610A?channelIndex=2&deviceIndex=0", ex.Endpoint?.ToString());
        Assert.AreEqual(42, ex.VendorCode);
        Assert.AreEqual(CanRecoverability.Retryable, ex.Recoverability);
        Assert.AreEqual("请选择支持 CAN 的 Vector 通道。", ex.Hint);
        Assert.AreEqual("0", ex.Details["deviceIndex"]);
        Assert.AreEqual("2", ex.Details["channelIndex"]);
    }

    // Fakes
    private sealed class FakeAdapterProvider : ICanAdapterProvider
    {
        public string AdapterId { get; }
        public string DisplayName { get; }
        public CanAdapterManifest Manifest { get; }
        public bool ScanCalled { get; private set; }
        public CanOpenContext? LastOpenContext { get; private set; }

        private readonly CanChannelScanResult? _scanResult;
        private readonly bool _throwOnScan;
        private readonly bool _throwOnCancellation;
        private readonly CanException? _scanException;

        public FakeAdapterProvider(
            string adapterId, string displayName, string[] schemes,
            bool supportsScan = false,
            CanChannelScanResult? scanResult = null,
            bool throwOnScan = false,
            bool throwOnCancellation = false,
            CanException? scanException = null)
        {
            AdapterId = adapterId;
            DisplayName = displayName;
            Manifest = new CanAdapterManifest(adapterId, displayName, schemes,
                supportsChannelScan: supportsScan);
            _scanResult = scanResult;
            _throwOnScan = throwOnScan;
            _throwOnCancellation = throwOnCancellation;
            _scanException = scanException;
        }

        public ValueTask<ICanBus> OpenAsync(CanOpenContext context, CancellationToken ct = default)
        {
            LastOpenContext = context;
            return ValueTask.FromResult<ICanBus>(new FakeBus(DisplayName));
        }

        public ValueTask<CanChannelScanResult> ScanAsync(
            ScanOptions? options = null, CancellationToken ct = default)
        {
            ScanCalled = true;
            if (_throwOnCancellation)
                ct.ThrowIfCancellationRequested();
            if (_scanException is not null)
                throw _scanException;
            if (_throwOnScan)
                throw new InvalidOperationException("Scan failed");
            return ValueTask.FromResult(_scanResult ?? new CanChannelScanResult());
        }
    }

    private sealed class FakeBus : ICanBus
    {
        public string DisplayName { get; }
        public bool IsOpen => true;
        public event Action<CanStatusEvent>? StatusChanged
        {
            add { }
            remove { }
        }
        public FakeBus(string name) => DisplayName = name;
        public ValueTask<CanTransmitSubmissionResult> SendAsync(CanFrame frame, CanTransmitOptions? options = null, CancellationToken ct = default)
            => ValueTask.FromResult(CanTransmitSubmissionResult.AcceptedResult(1));
        public ValueTask<CanTransmitSubmissionResult[]> SendBatchAsync(ReadOnlyMemory<CanFrame> frames, CanTransmitOptions? options = null, CancellationToken ct = default)
            => ValueTask.FromResult(Array.Empty<CanTransmitSubmissionResult>());
        public ICanSubscription Subscribe(CanSubscriptionOptions options) => throw new NotImplementedException();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
