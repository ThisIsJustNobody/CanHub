using CanHub;
using CanHub.Adapter.Zlg;
using Microsoft.Extensions.DependencyInjection;

var registry = CanHubRegistry.CreateDefault()
    .AddZlgAdapter();
var adapter = registry.FindAdapter("zlg");
Require(adapter is not null, "ZLG adapter was not registered.");
Require(adapter.Manifest.SupportsChannelScan, "ZLG manifest did not declare scan support.");

var services = new ServiceCollection();
services.AddCanHub()
    .AddZlgAdapter();
using (var serviceProvider = services.BuildServiceProvider())
{
    var diRegistry = serviceProvider.GetRequiredService<CanHubRegistry>();
    Require(diRegistry.FindAdapter("zlg") is not null, "ZLG adapter was not registered through DI.");
}

RequireFile("CanHub.Abstractions.dll");
RequireFile("CanHub.Core.dll");
RequireFile("CanHub.Adapter.Zlg.dll");

if (OperatingSystem.IsWindows())
{
    RequireFile("zlgcan.dll");
    RequireFile(Path.Combine("kerneldlls", "ZPSCANFD.dll"));
    RequireFile(Path.Combine("kerneldlls", "ZPS", "ZPSCANFD_IMPL.dll"));
}

Console.WriteLine("zlg-ok");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void RequireFile(string relativePath)
{
    var path = Path.Combine(AppContext.BaseDirectory, relativePath);
    if (!File.Exists(path))
        throw new FileNotFoundException($"Expected file was not copied to output: {relativePath}", path);
}
