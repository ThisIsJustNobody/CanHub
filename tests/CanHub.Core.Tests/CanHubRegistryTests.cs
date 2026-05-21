using CanHub;

namespace CanHub.Core.Tests;

[TestClass]
public sealed class CanHubRegistryTests
{
    private sealed class FakeBus : ICanBus
    {
        public string DisplayName => "FakeBus";
        public bool IsOpen => true;

        public event Action<CanStatusEvent>? StatusChanged
        {
            add { }
            remove { }
        }

        public ValueTask<CanTransmitSubmissionResult> SendAsync(
            CanFrame frame,
            CanTransmitOptions? options = null,
            CancellationToken ct = default)
        {
            return ValueTask.FromResult(CanTransmitSubmissionResult.AcceptedResult(1));
        }

        public ValueTask<CanTransmitSubmissionResult[]> SendBatchAsync(
            ReadOnlyMemory<CanFrame> frames,
            CanTransmitOptions? options = null,
            CancellationToken ct = default)
        {
            var results = new CanTransmitSubmissionResult[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                results[i] = CanTransmitSubmissionResult.AcceptedResult((ulong)(i + 1));
            return ValueTask.FromResult(results);
        }

        public ICanSubscription Subscribe(CanSubscriptionOptions options)
            => throw new NotImplementedException();

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAdapterProvider : ICanAdapterProvider
    {
        public FakeAdapterProvider(string adapterId, string displayName, string[] endpointSchemes)
        {
            Manifest = new CanAdapterManifest(adapterId, displayName, endpointSchemes);
        }

        public string AdapterId => Manifest.AdapterId;
        public string DisplayName => Manifest.DisplayName;
        public CanAdapterManifest Manifest { get; }

        public FakeBus? LastOpenedBus { get; private set; }
        public CanOpenContext? LastOpenedContext { get; private set; }

        public ValueTask<ICanBus> OpenAsync(CanOpenContext context, CancellationToken ct = default)
        {
            LastOpenedContext = context;
            LastOpenedBus = new FakeBus();
            return ValueTask.FromResult<ICanBus>(LastOpenedBus);
        }

        public ValueTask<CanChannelScanResult> ScanAsync(
            ScanOptions? options = null, CancellationToken ct = default)
        {
            throw new NotSupportedException("Fake adapter does not support channel scanning.");
        }
    }

    private sealed class NullManifestAdapterProvider : ICanAdapterProvider
    {
        public string AdapterId => "null-manifest";
        public string DisplayName => "Null Manifest";
        public CanAdapterManifest Manifest => null!;
        public ValueTask<ICanBus> OpenAsync(CanOpenContext context, CancellationToken ct = default)
            => throw new NotSupportedException();
        public ValueTask<CanChannelScanResult> ScanAsync(ScanOptions? options = null, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    [TestMethod(DisplayName = "默认注册表为空")]
    public void CreateDefault_ReturnsEmptyRegistry()
    {
        var registry = CanHubRegistry.CreateDefault();
        Assert.IsNotNull(registry);
        Assert.IsEmpty(registry.GetAdapters());
    }

    [TestMethod(DisplayName = "添加适配器后可查到")]
    public void AddAdapter_RegistersProvider()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("fake1", "Fake Adapter", ["fake"]);

        registry.AddAdapter(provider);

        var adapters = registry.GetAdapters();
        Assert.HasCount(1, adapters);
        Assert.AreEqual("fake1", adapters[0].AdapterId);
    }

    [TestMethod(DisplayName = "按协议方案查找适配器")]
    public void FindAdapter_ByScheme()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("fake1", "Fake Adapter", ["usb"]);

        registry.AddAdapter(provider);

        var found = registry.FindAdapter("usb");
        Assert.IsNotNull(found);
        Assert.AreEqual("fake1", found.AdapterId);
    }

    [TestMethod(DisplayName = "适配器查找不区分大小写")]
    public void FindAdapter_CaseInsensitive()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("fake1", "Fake Adapter", ["USB"]);

        registry.AddAdapter(provider);

        // CanEndpoint.Parse lowercases the scheme, so lookup should be case-insensitive
        var found = registry.FindAdapter("usb");
        Assert.IsNotNull(found);
        Assert.AreEqual("fake1", found.AdapterId);
    }

    [TestMethod(DisplayName = "查找不存在的适配器返回null")]
    public void FindAdapter_NotFound_ReturnsNull()
    {
        var registry = CanHubRegistry.CreateDefault();

        var found = registry.FindAdapter("nonexistent");
        Assert.IsNull(found);
    }

    [TestMethod(DisplayName = "重复方案注册抛出异常")]
    public void AddAdapter_DuplicateScheme_Throws()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider1 = new FakeAdapterProvider("fake1", "Adapter 1", ["usb"]);
        var provider2 = new FakeAdapterProvider("fake2", "Adapter 2", ["usb"]);

        registry.AddAdapter(provider1);

        var ex = Assert.ThrowsExactly<CanException>(() => registry.AddAdapter(provider2));
        Assert.AreEqual(CanErrorCategory.DuplicateAdapterScheme, ex.Category);
    }

    [TestMethod(DisplayName = "多方案注册失败时不会部分注册")]
    public void AddAdapter_MultiSchemeConflict_DoesNotPartiallyRegister()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider1 = new FakeAdapterProvider("fake1", "Adapter 1", ["pcan"]);
        var provider2 = new FakeAdapterProvider("fake2", "Adapter 2", ["usb", "pcan"]);

        registry.AddAdapter(provider1);

        var ex = Assert.ThrowsExactly<CanException>(() => registry.AddAdapter(provider2));

        Assert.AreEqual(CanErrorCategory.DuplicateAdapterScheme, ex.Category);
        Assert.IsNull(registry.FindAdapter("usb"));
        Assert.AreSame(provider1, registry.FindAdapter("pcan"));
    }

    [TestMethod(DisplayName = "适配器内部重复方案注册失败且无残留")]
    public void AddAdapter_DuplicateSchemeWithinProvider_DoesNotRegister()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("fake1", "Adapter 1", ["usb", "USB"]);

        var ex = Assert.ThrowsExactly<CanException>(() => registry.AddAdapter(provider));

        Assert.AreEqual(CanErrorCategory.DuplicateAdapterScheme, ex.Category);
        Assert.IsEmpty(registry.GetAdapters());
    }

    [TestMethod(DisplayName = "Manifest为空时注册失败且返回结构化异常")]
    public void AddAdapter_NullManifest_ThrowsCanException()
    {
        var registry = CanHubRegistry.CreateDefault();

        var ex = Assert.ThrowsExactly<CanException>(
            () => registry.AddAdapter(new NullManifestAdapterProvider()));

        Assert.AreEqual(CanErrorCategory.AdapterError, ex.Category);
        Assert.AreEqual("null-manifest", ex.AdapterId);
    }

    [TestMethod(DisplayName = "无效端点抛出异常")]
    public async Task OpenAsync_InvalidEndpoint_Throws()
    {
        var registry = CanHubRegistry.CreateDefault();

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await registry.OpenAsync("not-a-valid-uri", new CanOpenOptions(), TestContext.CancellationToken));
        Assert.AreEqual(CanErrorCategory.InvalidEndpoint, ex.Category);
    }

    [TestMethod(DisplayName = "无适配器时打开抛出异常")]
    public async Task OpenAsync_NoAdapter_Throws()
    {
        var registry = CanHubRegistry.CreateDefault();

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await registry.OpenAsync("usb://device1", new CanOpenOptions(), TestContext.CancellationToken));
        Assert.AreEqual(CanErrorCategory.AdapterNotFound, ex.Category);
    }

    [TestMethod(DisplayName = "CanEndpoint重载按方案打开并保留端点实例")]
    public async Task OpenAsync_CanEndpoint_RoutesBySchemeAndPreservesEndpoint()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("fake1", "Fake Adapter", ["usb"]);
        registry.AddAdapter(provider);
        var endpoint = CanEndpoint.Create("usb", "device1", channelIndex: 0);
        var openOptions = new CanOpenOptions();

        var bus = await registry.OpenAsync(endpoint, openOptions, TestContext.CancellationToken);

        Assert.IsNotNull(bus);
        Assert.IsNotNull(provider.LastOpenedContext);
        Assert.AreSame(endpoint, provider.LastOpenedContext.Endpoint);
        Assert.AreSame(openOptions, provider.LastOpenedContext.Options);
    }

    [TestMethod(DisplayName = "CanEndpoint无选项重载使用默认打开选项")]
    public async Task OpenAsync_CanEndpointWithoutOptions_UsesDefaultOptions()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("fake1", "Fake Adapter", ["usb"]);
        registry.AddAdapter(provider);
        var endpoint = CanEndpoint.Create("usb", "device1", channelIndex: 0);

        var bus = await registry.OpenAsync(endpoint, TestContext.CancellationToken);

        Assert.IsNotNull(bus);
        Assert.IsNotNull(provider.LastOpenedContext);
        Assert.AreSame(endpoint, provider.LastOpenedContext.Endpoint);
        Assert.AreEqual(CanBusParameters.Classic500k, provider.LastOpenedContext.Options.BusParameters);
    }

    [TestMethod(DisplayName = "CanEndpoint重载未知方案提前失败")]
    public async Task OpenAsync_CanEndpointUnknownScheme_Throws()
    {
        var registry = CanHubRegistry.CreateDefault();
        var endpoint = CanEndpoint.Create("usb", "device1");

        var ex = await Assert.ThrowsExactlyAsync<CanException>(
            async () => await registry.OpenAsync(endpoint, new CanOpenOptions(), TestContext.CancellationToken));

        Assert.AreEqual(CanErrorCategory.AdapterNotFound, ex.Category);
    }

    [TestMethod(DisplayName = "配置回调传递原生选项")]
    public async Task OpenAsync_WithConfigure_PassesNativeOptions()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("fake1", "Fake Adapter", ["usb"]);
        registry.AddAdapter(provider);

        var nativeOptions = new Dictionary<string, string> { ["bitrate"] = "500000" };
        var openOptions = new CanOpenOptions { NativeOptions = nativeOptions };
        var bus = await registry.OpenAsync("usb://device1?channel=0", openOptions, TestContext.CancellationToken);

        Assert.IsNotNull(bus);
        Assert.IsInstanceOfType(bus, typeof(FakeBus));
        Assert.IsNotNull(provider.LastOpenedContext);
        Assert.AreEqual("usb", provider.LastOpenedContext.Endpoint.Scheme);
        Assert.AreEqual("device1", provider.LastOpenedContext.Endpoint.Device);
        Assert.AreEqual(0, provider.LastOpenedContext.Endpoint.Channel);
        Assert.AreSame(nativeOptions, provider.LastOpenedContext.Options.NativeOptions);
    }

    [TestMethod(DisplayName = "无显式选项打开时使用默认Classic500k")]
    public async Task OpenAsync_WithoutOptions_UsesDefaultOptions()
    {
        var registry = CanHubRegistry.CreateDefault();
        var provider = new FakeAdapterProvider("fake1", "Fake Adapter", ["usb"]);
        registry.AddAdapter(provider);

        var bus = await registry.OpenAsync("usb://device1?channel=0", TestContext.CancellationToken);

        Assert.IsNotNull(bus);
        Assert.IsNotNull(provider.LastOpenedContext);
        Assert.AreEqual(CanBusParameters.Classic500k, provider.LastOpenedContext.Options.BusParameters);
        Assert.IsNull(provider.LastOpenedContext.Options.NativeOptions);
    }

    public TestContext TestContext { get; set; }
}
