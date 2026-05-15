using CanHub;
using CanHub.Adapter.Virtual;
using Microsoft.Extensions.DependencyInjection;

var registry = CanHubRegistry.CreateDefault()
    .AddVirtualAdapter();
Require(registry.FindAdapter("virtual") is not null, "Virtual adapter was not registered.");

var services = new ServiceCollection();
services.AddCanHub()
    .AddVirtualAdapter();
using (var serviceProvider = services.BuildServiceProvider())
{
    var diRegistry = serviceProvider.GetRequiredService<CanHubRegistry>();
    Require(diRegistry.FindAdapter("virtual") is not null, "Virtual adapter was not registered through DI.");
}

await using var bus = await registry.OpenAsync("virtual://nuget-smoke?channel=0");
Require(bus.IsOpen, "Virtual bus did not open.");

RequireFile("CanHub.Abstractions.dll");
RequireFile("CanHub.Core.dll");
RequireFile("CanHub.Adapter.Virtual.dll");

Console.WriteLine("virtual-ok");

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
