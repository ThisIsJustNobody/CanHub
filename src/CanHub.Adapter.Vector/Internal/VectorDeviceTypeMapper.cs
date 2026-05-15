using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 设备名模糊匹配。支持精确、后缀、包含匹配，大小写不敏感。<br/>
/// Vector device name fuzzy matching. Supports exact, suffix, and substring matching,
/// case-insensitive.
/// </summary>
internal static class VectorDeviceTypeMapper
{
    private static readonly (string Name, XLDefine.XL_HardwareType Type)[] s_knownTypes =
    [
        ("virtual", XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL),
        // VN56xx 系列 — 驱动配置统一报告为 A 型号，"VN5610" 也映射到 A
        ("VN5610A", XLDefine.XL_HardwareType.XL_HWTYPE_VN5610A),
        ("VN5610", XLDefine.XL_HardwareType.XL_HWTYPE_VN5610A),
        ("VN5620", XLDefine.XL_HardwareType.XL_HWTYPE_VN5620),
        ("VN5640", XLDefine.XL_HardwareType.XL_HWTYPE_VN5640),
        // VN16xx 系列
        ("VN1610", XLDefine.XL_HardwareType.XL_HWTYPE_VN1610),
        ("VN1630", XLDefine.XL_HardwareType.XL_HWTYPE_VN1630),
        ("VN1640", XLDefine.XL_HardwareType.XL_HWTYPE_VN1640),
        ("VN1611", XLDefine.XL_HardwareType.XL_HWTYPE_VN1611),
        // 其他系列
        ("VN2600", XLDefine.XL_HardwareType.XL_HWTYPE_VN2600),
        ("VN2610", XLDefine.XL_HardwareType.XL_HWTYPE_VN2610),
        ("VN2640", XLDefine.XL_HardwareType.XL_HWTYPE_VN2640),
        ("VN3300", XLDefine.XL_HardwareType.XL_HWTYPE_VN3300),
        ("VN3600", XLDefine.XL_HardwareType.XL_HWTYPE_VN3600),
        ("VN7600", XLDefine.XL_HardwareType.XL_HWTYPE_VN7600),
        ("VN8900", XLDefine.XL_HardwareType.XL_HWTYPE_VN8900),
        ("VN8950", XLDefine.XL_HardwareType.XL_HWTYPE_VN8950),
        ("CANcaseXL", XLDefine.XL_HardwareType.XL_HWTYPE_CANCASEXL),
        ("CANboardXL", XLDefine.XL_HardwareType.XL_HWTYPE_CANBOARDXL),
        ("CANcardX", XLDefine.XL_HardwareType.XL_HWTYPE_CANCARDX),
        ("CANcardXL", XLDefine.XL_HardwareType.XL_HWTYPE_CANCARDXL),
    ];

    /// <summary>
    /// 解析设备名到 XL_HardwareType。支持精确、后缀、包含匹配及枚举解析，失败时抛异常。<br/>
    /// Resolves a device name to XL_HardwareType. Supports exact, suffix, substring, and
    /// enum parsing; throws on failure.
    /// </summary>
    public static XLDefine.XL_HardwareType Resolve(string input)
    {
        if (TryResolve(input, out var type))
            return type;

        throw new CanException("vector", CanErrorCategory.InvalidEndpoint,
            $"Unknown Vector device type: '{input}'.");
    }

    /// <summary>
    /// 尝试解析设备名到 XL_HardwareType。成功返回 true，失败返回 false。<br/>
    /// Attempts to resolve a device name to XL_HardwareType. Returns true on success, false on failure.
    /// </summary>
    public static bool TryResolve(string input, out XLDefine.XL_HardwareType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var normalized = input.Trim();

        // 精确匹配（大小写不敏感）
        foreach (var (name, hwType) in s_knownTypes)
        {
            if (string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase))
            {
                type = hwType;
                return true;
            }
        }

        // 后缀匹配
        foreach (var (name, hwType) in s_knownTypes)
        {
            if (normalized.EndsWith(name, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(normalized, StringComparison.OrdinalIgnoreCase))
            {
                type = hwType;
                return true;
            }
        }

        var enumName = normalized.StartsWith("XL_HWTYPE_", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"XL_HWTYPE_{normalized}";
        if (Enum.TryParse(enumName, ignoreCase: true, out type))
            return true;

        // 包含匹配
        foreach (var (name, hwType) in s_knownTypes)
        {
            if (normalized.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            {
                type = hwType;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 将 XL_HardwareType 转换为端点中使用的设备名字符串。<br/>
    /// Converts XL_HardwareType to the device name string used in endpoints.
    /// </summary>
    public static string ToEndpointDeviceName(XLDefine.XL_HardwareType type)
    {
        foreach (var (name, hwType) in s_knownTypes)
        {
            if (hwType == type)
                return name;
        }

        const string prefix = "XL_HWTYPE_";
        var raw = type.ToString();
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? raw[prefix.Length..]
            : raw;
    }
}
