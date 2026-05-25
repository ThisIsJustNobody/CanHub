namespace CanHub.Adapter.Vector;

/// <summary>
/// Vector 适配器 vendor-specific 配置。总线参数应通过 CanBusParameters 配置。
/// 通过 <see cref="CanOpenOptions.NativeOptions"/> 传入。<br/>
/// Vector adapter vendor-specific configuration. Bus parameters should be configured
/// via CanBusParameters. Passed through <see cref="CanOpenOptions.NativeOptions"/>.
/// </summary>
public sealed class VectorOpenOptions : ICanNativeOptionsFingerprint
{
    /// <summary>
    /// 应用程序名称（显示在 Vector Hardware Config 中）。<br/>
    /// Application name (displayed in Vector Hardware Config).
    /// </summary>
    public string ApplicationName { get; set; } = "CanHub";

    /// <summary>
    /// 接收队列大小（帧数）。小于等于 0 时使用适配器默认值（Classic 256，FD 65536）。<br/>
    /// Receive queue size (frame count). When &lt;= 0, the adapter default is used (Classic 256, FD 65536).
    /// </summary>
    public int RxQueueSize { get; set; }

    /// <summary>
    /// 是否忽略外部应用的通道配置（Vector 特有 escape hatch）。<br/>
    /// 默认开启；配置调用因外部已激活通道返回 XL_ERR_INVALID_ACCESS 时会继续激活并上报警告。<br/>
    /// Whether to ignore channel configuration from other applications (Vector-specific escape hatch).
    /// Enabled by default; configuration calls returning XL_ERR_INVALID_ACCESS because another
    /// application already activated the channel are reported as warnings and activation continues.
    /// </summary>
    public bool IgnoreForeignConfiguration { get; set; } = true;

    /// <summary>
    /// 发送成功后回显确认帧（TX echo）。
    /// 启用后，接收路径中将出现 <c>XL_TRANSMIT_MSG</c> 事件。<br/>
    /// Echo confirmation frames after successful transmit (TX echo).
    /// When enabled, <c>XL_TRANSMIT_MSG</c> events appear in the receive path.
    /// </summary>
    public bool TransmitEcho { get; set; } = false;

    /// <summary>
    /// 发送缓冲区就绪时通知。
    /// 启用后，驱动在发送缓冲区可用时生成事件。<br/>
    /// Notify when the transmit buffer is ready.
    /// When enabled, the driver generates events when the transmit buffer is available.
    /// </summary>
    public bool ReadyToSendEvent { get; set; } = false;

    /// <summary>
    /// 抑制错误帧事件。设为 true 时不产生错误帧事件。<br/>
    /// Suppress error frame events. When set to true, error frame events are not generated.
    /// </summary>
    public bool SuppressErrorFrames { get; set; } = false;

    /// <summary>
    /// 抑制芯片状态事件。设为 true 时不产生芯片状态事件。<br/>
    /// Suppress chip state events. When set to true, chip state events are not generated.
    /// </summary>
    public bool SuppressChipState { get; set; } = false;

    /// <summary>
    /// CAN 采样模式：0 = 单次采样，1 = 三次采样（默认）。<br/>
    /// CAN sampling mode: 0 = single sampling, 1 = triple sampling (default).
    /// </summary>
    public byte Sam { get; set; } = 1;

    /// <summary>
    /// 获取用于租约冲突检测的指纹。序列化关键配置字段以生成确定性指纹。<br/>
    /// Gets the fingerprint used for lease conflict detection. Serializes key configuration
    /// fields to produce a deterministic fingerprint.
    /// </summary>
    public string GetFingerprint() =>
        string.Join('|',
        [
            $"ApplicationName={Uri.EscapeDataString(ApplicationName ?? string.Empty)}",
            $"RxQueueSize={RxQueueSize}",
            $"IgnoreForeignConfiguration={IgnoreForeignConfiguration}",
            $"TransmitEcho={TransmitEcho}",
            $"ReadyToSendEvent={ReadyToSendEvent}",
            $"SuppressErrorFrames={SuppressErrorFrames}",
            $"SuppressChipState={SuppressChipState}",
            $"Sam={Sam}",
        ]);
}
