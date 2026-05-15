using CanHub;
using CanHub.Adapter.Vector;
using Microsoft.Extensions.DependencyInjection;
using vxlapi_NET;

var registry = CanHubRegistry.CreateDefault()
    .AddVectorAdapter();
var adapter = registry.FindAdapter("vector");
Require(adapter is not null, "Vector adapter was not registered.");
Require(adapter.Manifest.SupportsChannelScan, "Vector manifest did not declare scan support.");

var services = new ServiceCollection();
services.AddCanHub()
    .AddVectorAdapter();
using (var serviceProvider = services.BuildServiceProvider())
{
    var diRegistry = serviceProvider.GetRequiredService<CanHubRegistry>();
    Require(diRegistry.FindAdapter("vector") is not null, "Vector adapter was not registered through DI.");
}

Require(typeof(XLDriver).FullName == "vxlapi_NET.XLDriver", "Vector managed wrapper assembly was not referenced.");

RequireFile("CanHub.Abstractions.dll");
RequireFile("CanHub.Core.dll");
RequireFile("CanHub.Adapter.Vector.dll");
RequireFile("vxlapi_NET.dll");

if (OperatingSystem.IsWindows())
    RequireFile(Environment.Is64BitProcess ? "vxlapi64.dll" : "vxlapi.dll");

Console.WriteLine("vector-ok");

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
