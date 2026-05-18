namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 通道生命周期原语。生产实现调用原生驱动，测试实现可模拟关闭和重开。<br/>
/// Vector channel lifecycle primitive. The production implementation calls the native driver; tests can simulate close and reopen.
/// </summary>
internal interface IVectorChannelLifecycle
{
    /// <summary>关闭通道端口。失败诊断通过 publishStatus 发布。<br/>Closes a channel port. Failure diagnostics are published through publishStatus.</summary>
    bool ClosePort(VectorChannelPort port, Action<CanStatusEvent> publishStatus);

    /// <summary>重新打开并激活通道端口。<br/>Reopens and activates a channel port.</summary>
    ValueTask OpenPortAsync(
        VectorChannelPort port,
        VectorChannelOpenSpec openSpec,
        CancellationToken ct = default);
}
