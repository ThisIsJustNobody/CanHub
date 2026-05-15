using Microsoft.Extensions.DependencyInjection;

namespace CanHub;

/// <summary>
/// CanHub DI 注册扩展方法。提供将 <see cref="CanHubRegistry"/> 和适配器注册到 <see cref="IServiceCollection"/> 的便捷方法。<br/>
/// CanHub DI registration extension methods. Provides convenience methods for registering <see cref="CanHubRegistry"/> and adapters with <see cref="IServiceCollection"/>.
/// </summary>
public static class CanHubServiceCollectionExtensions
{
    /// <summary>
    /// 注册 <see cref="CanHubRegistry"/> 单例到 DI 容器。<br/>
    /// Registers <see cref="CanHubRegistry"/> as a singleton in the DI container.
    /// </summary>
    /// <remarks>
    /// 工厂方法自动发现所有已注册的 <see cref="ICanAdapterProvider"/> 实例并添加到注册表中。注册表本身也注册为单例，确保整个应用生命周期内共享同一实例。<br/>
    /// The factory method auto-discovers all registered <see cref="ICanAdapterProvider"/> instances and adds them to the registry. The registry itself is also registered as a singleton, ensuring a single shared instance across the application lifetime.
    /// </remarks>
    /// <param name="services">服务集合。<br/>The service collection.</param>
    /// <returns>服务集合（支持链式调用）。<br/>The service collection (supports chaining).</returns>
    public static IServiceCollection AddCanHub(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<CanHubRegistry>(sp =>
        {
            var registry = CanHubRegistry.CreateDefault();
            foreach (var provider in sp.GetServices<ICanAdapterProvider>())
                registry.AddAdapter(provider);
            return registry;
        });
        return services;
    }

    /// <summary>
    /// 注册适配器提供者到 DI 容器。<br/>
    /// Registers an adapter provider in the DI container.
    /// </summary>
    /// <typeparam name="T">适配器提供者类型。<br/>The adapter provider type.</typeparam>
    /// <param name="services">服务集合。<br/>The service collection.</param>
    /// <returns>服务集合（支持链式调用）。<br/>The service collection (supports chaining).</returns>
    public static IServiceCollection AddCanHubAdapter<T>(this IServiceCollection services)
        where T : class, ICanAdapterProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICanAdapterProvider, T>();
        return services;
    }
}
