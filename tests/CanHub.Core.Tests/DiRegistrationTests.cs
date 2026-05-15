using Microsoft.Extensions.DependencyInjection;

namespace CanHub.Core.Tests;

[TestClass]
public sealed class DiRegistrationTests
{
    [TestMethod(DisplayName = "DI注册后Registry包含适配器")]
    public void AddCanHub_WithAdapter_FindsAdapterInRegistry()
    {
        var services = new ServiceCollection();
        services.AddCanHub();
        services.AddSingleton<ICanAdapterProvider, FakeAdapterProvider>();
        var sp = services.BuildServiceProvider();

        var registry = sp.GetRequiredService<CanHubRegistry>();
        var adapter = registry.FindAdapter("fake");

        Assert.IsNotNull(adapter);
        Assert.AreEqual("fake", adapter.AdapterId);
    }

    [TestMethod(DisplayName = "DI注册无适配器时Registry为空")]
    public void AddCanHub_WithoutAdapter_EmptyRegistry()
    {
        var services = new ServiceCollection();
        services.AddCanHub();
        var sp = services.BuildServiceProvider();

        var registry = sp.GetRequiredService<CanHubRegistry>();
        Assert.IsNull(registry.FindAdapter("nonexistent"));
    }
}

internal sealed class FakeAdapterProvider : ICanAdapterProvider
{
    public string AdapterId => "fake";
    public string DisplayName => "Fake Adapter";
    public CanAdapterManifest Manifest { get; } = new(
        "fake", "Fake", new[] { "fake" });
    public ValueTask<ICanBus> OpenAsync(CanOpenContext context, CancellationToken ct = default)
        => throw new NotSupportedException();
    public ValueTask<CanChannelScanResult> ScanAsync(
        ScanOptions? options = null, CancellationToken ct = default)
        => ValueTask.FromResult(new CanChannelScanResult());
}
