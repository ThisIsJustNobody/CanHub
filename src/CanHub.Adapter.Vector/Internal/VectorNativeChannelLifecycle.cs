namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// 基于 Vector XL Driver 的通道生命周期实现。<br/>
/// Vector XL Driver backed channel lifecycle implementation.
/// </summary>
internal sealed class VectorNativeChannelLifecycle : IVectorChannelLifecycle
{
    /// <summary>共享实例。<br/>Shared instance.</summary>
    public static VectorNativeChannelLifecycle Instance { get; } = new();

    private VectorNativeChannelLifecycle()
    {
    }

    /// <inheritdoc />
    public bool ClosePort(VectorChannelPort port, Action<CanStatusEvent> publishStatus) =>
        port.CloseForRecovery(publishStatus);

    /// <inheritdoc />
    public ValueTask OpenPortAsync(
        VectorChannelPort port,
        VectorChannelOpenSpec openSpec,
        CancellationToken ct = default) =>
        port.ReopenForRecoveryAsync(openSpec.Context, ct);
}
