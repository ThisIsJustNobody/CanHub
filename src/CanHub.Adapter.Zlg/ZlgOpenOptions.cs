namespace CanHub.Adapter.Zlg;

/// <summary>
/// ZLG 适配器专有打开选项。通用总线参数应通过 <see cref="CanBusParameters"/> 配置。<br/>
/// ZLG adapter-specific open options. General bus parameters should be configured via <see cref="CanBusParameters"/>.
/// </summary>
public sealed class ZlgOpenOptions : ICanNativeOptionsFingerprint
{
    /// <summary>
    /// 是否使用设备级合并接收。null 时由适配器根据设备能力选择，USBCANFD_200U 默认为 true。<br/>
    /// Whether to use device-level merged receive. When null, the adapter selects based on device capabilities; defaults to true for USBCANFD_200U.
    /// </summary>
    public bool? UseMergedReceive { get; set; }

    /// <summary>
    /// 设备工作模式。<br/>
    /// Device work mode.
    /// </summary>
    public ZlgWorkMode WorkMode { get; set; } = ZlgWorkMode.Normal;

    /// <summary>
    /// 默认发送类型。<br/>
    /// Default transmit type.
    /// </summary>
    public ZlgTransmitType DefaultTransmitType { get; set; } = ZlgTransmitType.Single;

    /// <summary>
    /// 验收码。<br/>
    /// Acceptance code.
    /// </summary>
    public uint AccCode { get; set; }

    /// <summary>
    /// 验收屏蔽码。<br/>
    /// Acceptance mask.
    /// </summary>
    public uint AccMask { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// 获取选项指纹，用于配置冲突检测。<br/>
    /// Gets the option fingerprint for configuration conflict detection.
    /// </summary>
    public string GetFingerprint() =>
        string.Join('|',
        [
            $"UseMergedReceive={UseMergedReceive}",
            $"WorkMode={WorkMode}",
            $"DefaultTransmitType={DefaultTransmitType}",
            $"AccCode={AccCode}",
            $"AccMask={AccMask}",
        ]);
}

/// <summary>
/// ZLG 设备工作模式。<br/>
/// ZLG device work mode.
/// </summary>
public enum ZlgWorkMode : byte
{
    /// <summary>
    /// 正常模式。<br/>
    /// Normal mode.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 静默模式（只收不发，不发送 ACK）。<br/>
    /// Silent mode (listen-only, does not send ACK).
    /// </summary>
    NotAck = 1,

    /// <summary>
    /// 自应答模式（自发自收）。<br/>
    /// Self-acknowledge mode (loopback).
    /// </summary>
    SelfAck = 2,

    /// <summary>
    /// 正常模式但不自动重试。<br/>
    /// Normal mode without automatic retry.
    /// </summary>
    NotRetry = 3,
}

/// <summary>
/// ZLG 发送类型。<br/>
/// ZLG transmit type.
/// </summary>
[Flags]
public enum ZlgTransmitType : uint
{
    /// <summary>
    /// 正常发送。<br/>
    /// Normal transmit.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 单次发送（不重试）。<br/>
    /// Single-shot transmit (no retry).
    /// </summary>
    Single = 1,

    /// <summary>
    /// 自应答发送。<br/>
    /// Self-acknowledge transmit.
    /// </summary>
    SelfAck = 2,

    /// <summary>
    /// 单次自应答发送。<br/>
    /// Single-shot self-acknowledge transmit.
    /// </summary>
    SingleSelfAck = Single | SelfAck,
}
