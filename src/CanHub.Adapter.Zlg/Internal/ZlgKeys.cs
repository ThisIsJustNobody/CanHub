namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 适配器内部键类型。用于设备/通道租约字典查找。<br/>
/// ZLG adapter internal key types. Used for device/channel lease dictionary lookups.
/// </summary>
internal readonly record struct ZlgDeviceKey(uint DeviceTypeId, int DeviceIndex)
{
    /// <summary>返回调试友好的字符串表示。<br/>Returns a debug-friendly string representation.</summary>
    public override string ToString() => $"{DeviceTypeId}:{DeviceIndex}";
}

/// <summary>
/// ZLG 通道键。包含设备类型 ID、设备索引和通道索引。<br/>
/// ZLG channel key. Contains device type ID, device index, and channel index.
/// </summary>
internal readonly record struct ZlgChannelKey(uint DeviceTypeId, int DeviceIndex, int ChannelIndex)
{
    /// <summary>派生设备键。<br/>Derived device key.</summary>
    public ZlgDeviceKey DeviceKey => new(DeviceTypeId, DeviceIndex);

    /// <summary>返回调试友好的字符串表示。<br/>Returns a debug-friendly string representation.</summary>
    public override string ToString() => $"{DeviceTypeId}:{DeviceIndex}:{ChannelIndex}";
}
