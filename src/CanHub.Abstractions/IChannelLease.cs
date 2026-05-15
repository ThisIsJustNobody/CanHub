namespace CanHub;

/// <summary>
/// 通道级租约接口。适配器实现此接口管理通道的激活和释放。<br/>
/// Channel-level lease interface. Adapters implement this interface to manage
/// channel activation and release.
/// </summary>
public interface IChannelLease : IAsyncDisposable
{
    /// <summary>通道索引。<br/>Channel index.</summary>
    int ChannelIndex { get; }

    /// <summary>当前引用计数。<br/>Current reference count.</summary>
    int ReferenceCount { get; }

    /// <summary>激活通道（增加引用计数）。<br/>Activates the channel (increments reference count).</summary>
    ValueTask ActivateAsync(CanOpenContext context, CancellationToken ct = default);

    /// <summary>停用通道（减少引用计数）。引用计数归零时应释放通道资源。<br/>
    /// Deactivates the channel (decrements reference count). Channel resources should
    /// be released when reference count reaches zero.</summary>
    /// <remarks>
    /// 当引用计数归零时，适配器应释放底层通道资源。多次停用（引用计数已为零）的具体行为由各适配器自行定义。<br/>
    /// When the reference count reaches zero, the adapter should release the underlying
    /// channel resources. The behavior of redundant deactivation (reference count already
    /// at zero) is adapter-defined.
    /// </remarks>
    ValueTask DeactivateAsync(CancellationToken ct = default);
}
