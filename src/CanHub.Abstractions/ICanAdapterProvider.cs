namespace CanHub;

/// <summary>
/// CAN 适配器提供者。用于发现适配器并打开总线。<br/>
/// CAN adapter provider. Used to discover adapters and open buses.
/// </summary>
public interface ICanAdapterProvider
{
    /// <summary>适配器唯一标识符。<br/>Unique adapter identifier.</summary>
    string AdapterId { get; }

    /// <summary>适配器显示名称。<br/>Adapter display name.</summary>
    string DisplayName { get; }

    /// <summary>适配器元数据清单。<br/>Adapter metadata manifest.</summary>
    CanAdapterManifest Manifest { get; }

    /// <summary>异步打开适配器并返回 CAN 总线句柄。<br/>Asynchronously opens the adapter and returns a CAN bus handle.</summary>
    ValueTask<ICanBus> OpenAsync(CanOpenContext context, CancellationToken ct = default);

    /// <summary>
    /// 扫描可用 CAN 通道。不支持扫描的适配器应抛出 <see cref="NotSupportedException"/>。<br/>
    /// Scans available CAN channels. Adapters that do not support scanning should throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    ValueTask<CanChannelScanResult> ScanAsync(
        ScanOptions? options = null, CancellationToken ct = default);
}
