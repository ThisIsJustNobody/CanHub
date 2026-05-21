using System.Collections.Concurrent;

namespace CanHub;

/// <summary>
/// CAN 适配器注册表。管理适配器提供者并按 scheme 打开总线。<br/>
/// CAN adapter registry. Manages adapter providers and opens buses by scheme.
/// </summary>
/// <remarks>
/// 每个 scheme 只能注册一个提供者；重复注册会抛出 <see cref="CanException"/>。线程安全。<br/>
/// Each scheme may only register one provider; duplicate registration throws <see cref="CanException"/>. Thread-safe.
/// </remarks>
public sealed class CanHubRegistry
{
    private readonly ConcurrentDictionary<string, ICanAdapterProvider> _adapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _adapterGate = new();

    private CanHubRegistry() { }

    /// <summary>创建默认的空注册表。<br/>Creates a default empty registry.</summary>
    public static CanHubRegistry CreateDefault() => new();

    /// <summary>
    /// 注册适配器提供者。每个 scheme 只能注册一次，重复注册抛出 <see cref="CanException"/>。<br/>
    /// Registers an adapter provider. Each scheme may only be registered once; duplicate registration throws <see cref="CanException"/>.
    /// </summary>
    /// <remarks>
    /// 使用锁保证线程安全；先校验 Manifest 中的 scheme 声明，再逐一插入字典。任何 scheme 已存在则整批回滚。<br/>
    /// Thread-safe via lock; validates Manifest scheme declarations first, then inserts into the dictionary atomically. Rolls back entirely if any scheme already exists.
    /// </remarks>
    /// <param name="provider">适配器提供者。<br/>The adapter provider.</param>
    /// <returns>当前注册表（支持链式调用）。<br/>Current registry (supports chaining).</returns>
    /// <exception cref="ArgumentNullException">provider 为 null。<br/>provider is null.</exception>
    /// <exception cref="CanException">provider.AdapterId 或 Manifest.EndpointSchemes 为空，或 scheme 已被注册。<br/>provider.AdapterId or Manifest.EndpointSchemes is empty, or a scheme is already registered.</exception>
    public CanHubRegistry AddAdapter(ICanAdapterProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrEmpty(provider.AdapterId))
            throw new CanException("*", CanErrorCategory.AdapterError, "适配器 ID 不能为空。");

        var manifest = provider.Manifest
            ?? throw new CanException(provider.AdapterId, CanErrorCategory.AdapterError, "适配器未声明 Manifest。");

        if (manifest.EndpointSchemes.Count == 0)
            throw new CanException(provider.AdapterId, CanErrorCategory.AdapterError, "适配器未声明任何端点方案。");

        var schemes = manifest.EndpointSchemes
            .Select(s => s?.Trim())
            .ToArray();

        foreach (var scheme in schemes)
        {
            if (string.IsNullOrEmpty(scheme))
                throw new CanException(provider.AdapterId, CanErrorCategory.AdapterError, "适配器端点方案不能为空。");
        }

        var duplicateScheme = schemes
            .GroupBy(s => s!, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateScheme is not null)
        {
            throw new CanException(
                provider.AdapterId,
                CanErrorCategory.DuplicateAdapterScheme,
                $"适配器重复声明端点方案 '{duplicateScheme.Key}'。");
        }

        lock (_adapterGate)
        {
            foreach (var scheme in schemes)
            {
                if (_adapters.TryGetValue(scheme!, out var existing))
                {
                    throw new CanException(
                        provider.AdapterId,
                        CanErrorCategory.DuplicateAdapterScheme,
                        $"端点方案 '{scheme}' 已被适配器 '{existing.AdapterId}' 注册。");
                }
            }

            foreach (var scheme in schemes)
            {
                _adapters[scheme!] = provider;
            }
        }

        return this;
    }

    /// <summary>
    /// 按 scheme 查找适配器提供者。未找到返回 null。<br/>
    /// Finds an adapter provider by scheme. Returns null if not found.
    /// </summary>
    /// <param name="scheme">端点方案（不区分大小写）。<br/>The endpoint scheme (case-insensitive).</param>
    /// <returns>匹配的适配器提供者，或 null。<br/>The matching adapter provider, or null.</returns>
    public ICanAdapterProvider? FindAdapter(string scheme)
    {
        _adapters.TryGetValue(scheme, out var provider);
        return provider;
    }

    /// <summary>
    /// 获取所有已注册的适配器提供者（按 AdapterId 去重）。<br/>
    /// Gets all registered adapter providers (deduplicated by AdapterId).
    /// </summary>
    public IReadOnlyList<ICanAdapterProvider> GetAdapters()
        => _adapters.Values.DistinctBy(p => p.AdapterId).ToList();

    /// <summary>
    /// 扫描所有已注册适配器的可用 CAN 通道。<br/>
    /// Scans all registered adapters for available CAN channels.
    /// 跳过 SupportsChannelScan=false 的适配器，某个适配器扫描失败时记录诊断并继续。<br/>
    /// Skips adapters with SupportsChannelScan=false; records diagnostics and continues on individual adapter scan failures.
    /// </summary>
    public ValueTask<CanChannelScanResult> ScanAsync(
        ScanOptions? options = null, CancellationToken ct = default)
    {
        return ScanAsyncCore(null, options, ct);
    }

    /// <summary>
    /// 按筛选条件扫描已注册适配器的可用 CAN 通道。<br/>
    /// Scans registered adapters for available CAN channels using the specified filter.
    /// </summary>
    public ValueTask<CanChannelScanResult> ScanAsync(
        Func<ICanAdapterProvider, bool> filter,
        ScanOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return ScanAsyncCore(filter, options, ct);
    }

    private async ValueTask<CanChannelScanResult> ScanAsyncCore(
        Func<ICanAdapterProvider, bool>? filter,
        ScanOptions? options,
        CancellationToken ct)
    {
        var channels = new List<CanChannelInfo>();
        var diagnostics = new List<ScanDiagnostic>();

        foreach (var provider in _adapters.Values.DistinctBy(p => p.AdapterId))
        {
            if (filter is not null && !filter(provider))
                continue;

            var manifest = provider.Manifest;
            if (!manifest.SupportsChannelScan)
                continue;

            try
            {
                var result = await provider.ScanAsync(options, ct);
                channels.AddRange(result.Channels);
                diagnostics.AddRange(result.Diagnostics);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CanException ex)
            {
                diagnostics.Add(new ScanDiagnostic(
                    ex.Category,
                    ex.Message,
                    ex.VendorCode,
                    ex.Recoverability,
                    provider.AdapterId,
                    ex.Endpoint?.ToString(),
                    ex.Hint,
                    ex.Details));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new ScanDiagnostic(
                    CanErrorCategory.AdapterError,
                    $"Scan failed for adapter '{provider.AdapterId}': {ex.GetType().Name}: {ex.Message}",
                    adapterId: provider.AdapterId));
            }
        }

        return new CanChannelScanResult(channels, diagnostics);
    }

    /// <summary>
    /// 通过端点 URI 异步打开 CAN 总线。<br/>
    /// Asynchronously opens a CAN bus via an endpoint URI.
    /// </summary>
    /// <remarks>
    /// 委托至 <see cref="OpenAsync(string, CanOpenOptions, CancellationToken)"/>，使用默认 <see cref="CanOpenOptions"/>。<br/>
    /// Delegates to <see cref="OpenAsync(string, CanOpenOptions, CancellationToken)"/> with default <see cref="CanOpenOptions"/>.
    /// </remarks>
    /// <param name="endpoint">端点 URI（推荐格式：scheme://device?channelIndex=N，兼容旧 channel 参数）。<br/>The endpoint URI (recommended format: scheme://device?channelIndex=N, with legacy channel compatibility).</param>
    /// <param name="ct">取消令牌。<br/>The cancellation token.</param>
    /// <returns>打开的 CAN 总线句柄。<br/>The opened CAN bus handle.</returns>
    public ValueTask<ICanBus> OpenAsync(
        string endpoint, CancellationToken ct = default)
        => OpenAsync(endpoint, new CanOpenOptions(), ct);

    /// <summary>
    /// 通过已解析端点异步打开 CAN 总线。<br/>
    /// Asynchronously opens a CAN bus via an already parsed endpoint.
    /// </summary>
    /// <remarks>
    /// 委托至 <see cref="OpenAsync(CanEndpoint, CanOpenOptions, CancellationToken)"/>，使用默认 <see cref="CanOpenOptions"/>。<br/>
    /// Delegates to <see cref="OpenAsync(CanEndpoint, CanOpenOptions, CancellationToken)"/> with default <see cref="CanOpenOptions"/>.
    /// </remarks>
    public ValueTask<ICanBus> OpenAsync(
        CanEndpoint endpoint, CancellationToken ct = default)
        => OpenAsync(endpoint, new CanOpenOptions(), ct);

    /// <summary>
    /// 通过扫描得到的通道信息异步打开 CAN 总线。<br/>
    /// Asynchronously opens a CAN bus via a scanned channel info.
    /// </summary>
    /// <remarks>
    /// 委托至 <see cref="OpenAsync(CanChannelInfo, CanOpenOptions, CancellationToken)"/>，使用默认 <see cref="CanOpenOptions"/>。<br/>
    /// Delegates to <see cref="OpenAsync(CanChannelInfo, CanOpenOptions, CancellationToken)"/> with default <see cref="CanOpenOptions"/>.
    /// </remarks>
    public ValueTask<ICanBus> OpenAsync(
        CanChannelInfo channel, CancellationToken ct = default)
        => OpenAsync(channel, new CanOpenOptions(), ct);

    /// <summary>
    /// 通过扫描得到的通道信息和显式打开选项异步打开 CAN 总线。<br/>
    /// Asynchronously opens a CAN bus via a scanned channel info and explicit open options.
    /// </summary>
    /// <remarks>
    /// 校验通道的 <see cref="CanChannelInfo.CanOpen"/> 状态，不可用则抛出 <see cref="CanException"/>。最终委托至 <see cref="OpenAsync(string, CanOpenOptions, CancellationToken)"/>。<br/>
    /// Validates the channel's <see cref="CanChannelInfo.CanOpen"/> status; throws <see cref="CanException"/> if not openable. Delegates to <see cref="OpenAsync(string, CanOpenOptions, CancellationToken)"/>.
    /// </remarks>
    public ValueTask<ICanBus> OpenAsync(
        CanChannelInfo channel, CanOpenOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(options);

        if (!channel.CanOpen || string.IsNullOrWhiteSpace(channel.Endpoint))
        {
            if (channel.Diagnostic is { } diagnostic)
            {
                throw new CanException(
                    channel.AdapterId,
                    diagnostic.Category,
                    diagnostic.Message,
                    endpoint: TryParseEndpoint(diagnostic.Endpoint),
                    vendorCode: diagnostic.NativeErrorCode,
                    recoverability: diagnostic.Recoverability,
                    hint: diagnostic.Hint,
                    details: diagnostic.Details);
            }

            throw new CanException(
                channel.AdapterId,
                CanErrorCategory.InvalidEndpoint,
                $"Scanned channel '{channel.DeviceName}[{channel.DeviceIndex}] channel {channel.ChannelIndex}' cannot be opened.");
        }

        return OpenAsync(channel.Endpoint, options, ct);
    }

    /// <summary>
    /// 通过端点 URI 和显式打开选项异步打开 CAN 总线。<br/>
    /// Asynchronously opens a CAN bus via an endpoint URI and explicit open options.
    /// </summary>
    /// <remarks>
    /// 解析端点 URI 提取 scheme，查找对应的适配器提供者，然后委托打开。未找到适配器时抛出 <see cref="CanException"/>（<see cref="CanErrorCategory.AdapterNotFound"/>）。<br/>
    /// Parses the endpoint URI to extract the scheme, looks up the matching adapter provider, then delegates opening. Throws <see cref="CanException"/> (<see cref="CanErrorCategory.AdapterNotFound"/>) if no adapter matches.
    /// </remarks>
    /// <param name="endpoint">端点 URI（推荐格式：scheme://device?channelIndex=N，兼容旧 channel 参数）。<br/>The endpoint URI (recommended format: scheme://device?channelIndex=N, with legacy channel compatibility).</param>
    /// <param name="options">打开选项（BusParameters 默认 Classic500k）。<br/>Open options (BusParameters defaults to Classic500k).</param>
    /// <param name="ct">取消令牌。<br/>The cancellation token.</param>
    /// <returns>打开的 CAN 总线句柄。<br/>The opened CAN bus handle.</returns>
    public ValueTask<ICanBus> OpenAsync(
        string endpoint, CanOpenOptions options, CancellationToken ct = default)
    {
        var parsed = CanEndpoint.Parse(endpoint);
        return OpenAsync(parsed, options, ct);
    }

    /// <summary>
    /// 通过已解析端点和显式打开选项异步打开 CAN 总线。<br/>
    /// Asynchronously opens a CAN bus via an already parsed endpoint and explicit open options.
    /// </summary>
    /// <remarks>
    /// 按端点 scheme 查找对应的适配器提供者，然后委托打开。未找到适配器时抛出 <see cref="CanException"/>（<see cref="CanErrorCategory.AdapterNotFound"/>）。<br/>
    /// Looks up the matching adapter provider by endpoint scheme, then delegates opening. Throws <see cref="CanException"/> (<see cref="CanErrorCategory.AdapterNotFound"/>) if no adapter matches.
    /// </remarks>
    /// <param name="endpoint">已解析端点。<br/>The parsed endpoint.</param>
    /// <param name="options">打开选项（BusParameters 默认 Classic500k）。<br/>Open options (BusParameters defaults to Classic500k).</param>
    /// <param name="ct">取消令牌。<br/>The cancellation token.</param>
    /// <returns>打开的 CAN 总线句柄。<br/>The opened CAN bus handle.</returns>
    public ValueTask<ICanBus> OpenAsync(
        CanEndpoint endpoint, CanOpenOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(options);

        var provider = FindAdapter(endpoint.Scheme);
        if (provider is null)
            throw new CanException("*", CanErrorCategory.AdapterNotFound, $"找不到端点方案 '{endpoint.Scheme}' 对应的适配器。");

        var context = new CanOpenContext(endpoint, options);
        return provider.OpenAsync(context, ct);
    }

    private static CanEndpoint? TryParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        try
        {
            return CanEndpoint.Parse(endpoint);
        }
        catch (CanException)
        {
            return null;
        }
    }
}
