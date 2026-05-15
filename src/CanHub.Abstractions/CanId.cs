using System.Globalization;

namespace CanHub;

/// <summary>
/// CAN 帧标识符，支持标准 11 位和扩展 29 位 ID。<br/>
/// CAN frame identifier, supporting standard 11-bit and extended 29-bit IDs.
/// </summary>
public readonly record struct CanId
{
    /// <summary>标准 CAN ID 最大值（11 位）。<br/>Standard CAN ID maximum (11-bit).</summary>
    public const uint MaxStandard = 0x7FF;

    /// <summary>扩展 CAN ID 最大值（29 位）。<br/>Extended CAN ID maximum (29-bit).</summary>
    public const uint MaxExtended = 0x1FFFFFFF;

    /// <summary>原始 ID 值。<br/>Raw ID value.</summary>
    public uint Value { get; }

    /// <summary>是否为扩展 ID（29 位）。<br/>Whether this is an extended ID (29-bit).</summary>
    public bool IsExtended { get; }

    /// <summary>创建标准 CAN ID。<br/>Create a standard CAN ID.</summary>
    public CanId(uint value)
    {
        if (value > MaxStandard)
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Standard CAN ID cannot exceed 0x{MaxStandard:X}.");
        Value = value;
        IsExtended = false;
    }

    /// <summary>使用显式扩展标志创建 CAN ID。<br/>Create a CAN ID with explicit extended flag.</summary>
    public CanId(uint value, bool isExtended)
    {
        uint max = isExtended ? MaxExtended : MaxStandard;
        if (value > max)
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"{(isExtended ? "Extended" : "Standard")} CAN ID cannot exceed 0x{max:X}.");
        Value = value;
        IsExtended = isExtended;
    }

    /// <summary>创建标准 CAN ID。<br/>Create a standard CAN ID.</summary>
    public static CanId Standard(uint value) => new(value, false);

    /// <summary>创建扩展 CAN ID。<br/>Create an extended CAN ID.</summary>
    public static CanId Extended(uint value) => new(value, true);

    /// <summary>
    /// 格式化为 "0x{Value:X}"，扩展帧追加 "x" 后缀。示例：标准帧 "0x100"，扩展帧 "0x100000x"。<br/>
    /// Formats as "0x{Value:X}", appending "x" suffix for extended frames.
    /// Example: "0x100" for standard, "0x100000x" for extended.
    /// </summary>
    public override string ToString() =>
        IsExtended ? $"0x{Value:X}x" : $"0x{Value:X}";

    /// <summary>
    /// 从字符串解析 CanId。支持 "0x100"（标准帧）和 "0x100000x"（扩展帧）格式。<br/>
    /// Parses a CanId from a string. Supports "0x100" (standard) and "0x100000x" (extended) formats.
    /// </summary>
    public static CanId Parse(ReadOnlySpan<char> text)
    {
        if (TryParse(text, out var id))
            return id;
        throw new FormatException($"无法解析 CAN ID: \"{text.ToString()}\"。");
    }

    /// <summary>
    /// 尝试从字符串解析 CanId。支持 "0x100"（标准帧）和 "0x100000x"（扩展帧）格式。<br/>
    /// Attempts to parse a CanId from a string. Supports "0x100" (standard) and "0x100000x" (extended) formats.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out CanId id)
    {
        id = default;

        if (text.Length < 1)
            return false;

        bool extended = text[^1] is 'x' or 'X';
        var hexSpan = extended ? text[..^1] : text;

        if (!hexSpan.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            !hexSpan.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!uint.TryParse(hexSpan[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            return false;

        if (extended)
        {
            if (value > MaxExtended) return false;
            id = new CanId(value, true);
        }
        else
        {
            if (value > MaxStandard) return false;
            id = new CanId(value, false);
        }

        return true;
    }
}
