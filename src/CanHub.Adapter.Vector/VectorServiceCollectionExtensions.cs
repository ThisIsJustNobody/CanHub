using Microsoft.Extensions.DependencyInjection;

namespace CanHub.Adapter.Vector;

/// <summary>
/// Vector 适配器 DI 注册扩展方法。<br/>
/// DI registration extension methods for the Vector adapter.
/// </summary>
public static class VectorServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Vector CAN 适配器到 CanHub 注册表。<br/>
    /// Registers the Vector CAN adapter with the CanHub registry.
    /// </summary>
    /// <param name="registry">CanHub 注册表。 / The CanHub registry.</param>
    /// <returns>注册表（支持链式调用）。 / The registry (supports chaining).</returns>
    public static CanHubRegistry AddVectorAdapter(this CanHubRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.AddAdapter(new VectorAdapterProvider());
    }

    /// <summary>
    /// 注册 Vector CAN 适配器到 DI 容器。<br/>
    /// Registers the Vector CAN adapter with the DI container.
    /// </summary>
    /// <param name="services">服务集合。 / The service collection.</param>
    /// <returns>服务集合（支持链式调用）。 / The service collection (supports chaining).</returns>
    public static IServiceCollection AddVectorAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return global::CanHub.CanHubServiceCollectionExtensions.AddCanHubAdapter<VectorAdapterProvider>(services);
    }
}
