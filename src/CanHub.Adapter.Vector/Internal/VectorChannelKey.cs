using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 通道的唯一标识键。由硬件类型、设备索引和通道索引组成。<br/>
/// Unique identification key for a Vector channel. Composed of hardware type, device index,
/// and channel index.
/// </summary>
internal readonly record struct VectorChannelKey(
    XLDefine.XL_HardwareType DeviceType,
    int DeviceIndex,
    int ChannelIndex)
{
    /// <summary>
    /// 返回通道键的字符串表示，格式为 {DeviceType}:{DeviceIndex}:{ChannelIndex}。<br/>
    /// Returns a string representation of the channel key in the format {DeviceType}:{DeviceIndex}:{ChannelIndex}.
    /// </summary>
    public override string ToString() => $"{DeviceType}:{DeviceIndex}:{ChannelIndex}";
}
