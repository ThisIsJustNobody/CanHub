using Microsoft.Extensions.DependencyInjection;

namespace CanHub.Adapter.Zlg;

/// <summary>
/// ZLG 适配器 DI 注册扩展方法。<br/>
/// Extension methods for registering the ZLG adapter with DI and the CanHub registry.
/// </summary>
public static class ZlgServiceCollectionExtensions
{
    /// <summary>
    /// 注册 ZLG CAN 适配器到 CanHub 注册表。<br/>
    /// Registers the ZLG CAN adapter with the CanHub registry.
    /// </summary>
    public static CanHubRegistry AddZlgAdapter(this CanHubRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.AddAdapter(new ZlgAdapterProvider());
    }

    /// <summary>
    /// 注册 ZLG CAN 适配器到 DI 容器。<br/>
    /// Registers the ZLG CAN adapter with the DI container.
    /// </summary>
    public static IServiceCollection AddZlgAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return global::CanHub.CanHubServiceCollectionExtensions.AddCanHubAdapter<ZlgAdapterProvider>(services);
    }
}
