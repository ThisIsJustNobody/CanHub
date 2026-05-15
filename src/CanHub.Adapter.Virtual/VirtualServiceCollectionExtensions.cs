using Microsoft.Extensions.DependencyInjection;

namespace CanHub.Adapter.Virtual;

/// <summary>
/// Virtual 适配器的 DI 注册扩展方法。<br/>
/// DI registration extension methods for the Virtual adapter.
/// </summary>
public static class VirtualServiceCollectionExtensions
{
    /// <summary>
    /// 将 Virtual CAN 适配器注册到 CanHubRegistry 中。<br/>
    /// Registers the Virtual CAN adapter into the CanHubRegistry.
    /// </summary>
    public static CanHubRegistry AddVirtualAdapter(this CanHubRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.AddAdapter(new VirtualAdapterProvider());
    }

    /// <summary>
    /// 将 Virtual CAN 适配器注册到 DI 容器中。<br/>
    /// Registers the Virtual CAN adapter into the DI container.
    /// </summary>
    public static IServiceCollection AddVirtualAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return global::CanHub.CanHubServiceCollectionExtensions.AddCanHubAdapter<VirtualAdapterProvider>(services);
    }
}
