namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 解析后的打开选项。合并用户配置和 CanBusParameters 后的最终打开配置。<br/>
/// ZLG resolved open options. The final open configuration after merging user settings with CanBusParameters.
/// </summary>
internal sealed class ZlgResolvedOpenOptions : ICanNativeOptionsFingerprint
{
    /// <summary>是否使用设备级合并接收。<br/>Whether to use device-level merged receive.</summary>
    public required bool UseMergedReceive { get; init; }

    /// <summary>设备工作模式。<br/>Device work mode.</summary>
    public required ZlgWorkMode WorkMode { get; init; }

    /// <summary>默认发送类型。<br/>Default transmit type.</summary>
    public required ZlgTransmitType DefaultTransmitType { get; init; }

    /// <summary>验收码。<br/>Acceptance code.</summary>
    public required uint AccCode { get; init; }

    /// <summary>验收屏蔽码。<br/>Acceptance mask.</summary>
    public required uint AccMask { get; init; }

    /// <summary>
    /// 创建解析后的打开选项。合并 CanBusParameters 和 ZlgOpenOptions，处理工作模式冲突。<br/>
    /// Creates resolved open options. Merges CanBusParameters with ZlgOpenOptions and handles work mode conflicts.
    /// </summary>
    public static ZlgResolvedOpenOptions Create(
        ZlgDeviceCapabilities capabilities,
        CanBusParameters busParameters,
        object? nativeOptions)
    {
        if (nativeOptions is not null and not global::CanHub.Adapter.Zlg.ZlgOpenOptions)
        {
            throw new CanException("zlg", CanErrorCategory.ConfigurationConflict,
                "ZLG adapter NativeOptions must be ZlgOpenOptions when specified.");
        }

        var options = (global::CanHub.Adapter.Zlg.ZlgOpenOptions?)nativeOptions;
        var workMode = ResolveWorkMode(busParameters, options?.WorkMode ?? ZlgWorkMode.Normal);

        return new ZlgResolvedOpenOptions
        {
            UseMergedReceive = options?.UseMergedReceive ?? capabilities.SupportsMergedReceive,
            WorkMode = workMode,
            DefaultTransmitType = options?.DefaultTransmitType ?? global::CanHub.Adapter.Zlg.ZlgTransmitType.Single,
            AccCode = options?.AccCode ?? 0,
            AccMask = options?.AccMask ?? 0xFFFFFFFF,
        };
    }

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

    private static global::CanHub.Adapter.Zlg.ZlgWorkMode ResolveWorkMode(
        CanBusParameters busParameters,
        global::CanHub.Adapter.Zlg.ZlgWorkMode configuredWorkMode)
    {
        if (busParameters.AckOff == true && busParameters.SelfAck == true)
        {
            throw new CanException("zlg", CanErrorCategory.ConfigurationConflict,
                "CanBusParameters.AckOff and SelfAck cannot both be true for ZLG.");
        }

        var desired = configuredWorkMode;
        if (busParameters.AckOff == true)
            desired = MergeWorkMode(configuredWorkMode, global::CanHub.Adapter.Zlg.ZlgWorkMode.NotAck, nameof(CanBusParameters.AckOff));
        if (busParameters.SelfAck == true)
            desired = MergeWorkMode(configuredWorkMode, global::CanHub.Adapter.Zlg.ZlgWorkMode.SelfAck, nameof(CanBusParameters.SelfAck));

        if (busParameters.AckOff == false && desired == global::CanHub.Adapter.Zlg.ZlgWorkMode.NotAck)
        {
            throw new CanException("zlg", CanErrorCategory.ConfigurationConflict,
                "CanBusParameters.AckOff=false conflicts with ZlgOpenOptions.WorkMode=NotAck.");
        }

        if (busParameters.SelfAck == false && desired == global::CanHub.Adapter.Zlg.ZlgWorkMode.SelfAck)
        {
            throw new CanException("zlg", CanErrorCategory.ConfigurationConflict,
                "CanBusParameters.SelfAck=false conflicts with ZlgOpenOptions.WorkMode=SelfAck.");
        }

        return desired;
    }

    private static global::CanHub.Adapter.Zlg.ZlgWorkMode MergeWorkMode(
        global::CanHub.Adapter.Zlg.ZlgWorkMode configured,
        global::CanHub.Adapter.Zlg.ZlgWorkMode requested,
        string parameterName)
    {
        if (configured is global::CanHub.Adapter.Zlg.ZlgWorkMode.Normal || configured == requested)
            return requested;

        throw new CanException("zlg", CanErrorCategory.ConfigurationConflict,
            $"{parameterName} conflicts with ZlgOpenOptions.WorkMode={configured}.");
    }
}
