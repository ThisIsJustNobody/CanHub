using CanHub;
using CanHub.Adapter.Vector;
using Microsoft.Extensions.DependencyInjection;
using vxlapi_NET;

var registry = CanHubRegistry.CreateDefault()
    .AddVectorAdapter();
var adapter = registry.FindAdapter("vector");
Require(adapter is not null, "Vector adapter was not registered.");
Require(adapter!.Manifest.SupportsChannelScan, "Vector manifest did not declare scan support.");

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
{
    var archFolder = Environment.Is64BitProcess ? "x64" : "x86";
    var nativeFile = Environment.Is64BitProcess ? "vxlapi64.dll" : "vxlapi.dll";
    RequireFile(Path.Combine("canhub", "vector", archFolder, nativeFile));
    RequireMissingFile("vxlapi64.dll");
    RequireMissingFile("vxlapi.dll");
    Require(new XLDriver().XL_GetErrorString(XLDefine.XL_Status.XL_SUCCESS) == "XL_SUCCESS",
        "Vector native DLL was not resolved from the namespaced output folder.");
}

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

static void RequireMissingFile(string relativePath)
{
    var path = Path.Combine(AppContext.BaseDirectory, relativePath);
    if (File.Exists(path))
        throw new InvalidOperationException($"Unexpected file was copied to output root: {relativePath}");
}
