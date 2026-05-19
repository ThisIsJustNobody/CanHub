namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 通道生命周期原语。生产实现调用原生驱动，测试实现可模拟关闭和重开。<br/>
/// ZLG channel lifecycle primitive. The production implementation calls the native driver; tests can simulate close and reopen.
/// </summary>
internal interface IZlgChannelLifecycle
{
    /// <summary>打开并启动通道，返回新的通道句柄。<br/>Opens and starts a channel, returning the new channel handle.</summary>
    nint OpenChannel(ZlgDeviceLeaseEntry device, ZlgChannelOpenSpec spec);

    /// <summary>关闭通道句柄。失败诊断通过 publishStatus 发布。<br/>Closes a channel handle. Failure diagnostics are published through publishStatus.</summary>
    bool CloseChannel(nint channelHandle, int channelIndex, Action<CanStatusEvent> publishStatus);
}
