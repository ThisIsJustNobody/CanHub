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
    var archFolder = Environment.Is64BitProcess ? "x64" : "x86";
    RequireFile(Path.Combine("canhub", "zlg", archFolder, "zlgcan.dll"));
    RequireFile(Path.Combine("canhub", "zlg", archFolder, "kerneldlls", "ZPSCANFD.dll"));
    RequireFile(Path.Combine("canhub", "zlg", archFolder, "kerneldlls", "ZPS", "ZPSCANFD_IMPL.dll"));
    RequireMissingFile("zlgcan.dll");
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

static void RequireMissingFile(string relativePath)
{
    var path = Path.Combine(AppContext.BaseDirectory, relativePath);
    if (File.Exists(path))
        throw new InvalidOperationException($"Expected file to stay out of output root: {relativePath}");
}
