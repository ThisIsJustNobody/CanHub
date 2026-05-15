using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CanHub;

/// <summary>
/// 表示一个完整的 Classic CAN 或 CAN FD 帧。只读结构体，内联负载存储（最多 64 字节），零堆分配。<br/>
/// Represents a complete Classic CAN or CAN FD frame. Readonly struct with inline payload storage (up to 64 bytes), zero heap allocation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct CanFrame : IEquatable<CanFrame>
{
    /// <summary>最大负载长度（CAN FD）。<br/>Maximum payload length (CAN FD).</summary>
    public const int MaxPayloadLength = 64;

    /// <summary>Classic CAN 最大负载长度。<br/>Classic CAN maximum payload length.</summary>
    public const int MaxClassicPayloadLength = 8;

    private readonly ulong _b0, _b1, _b2, _b3, _b4, _b5, _b6, _b7;

    /// <summary>帧标识符。仅对数据帧和远程帧有意义。<br/>Frame identifier. Meaningful only for data and remote frames.</summary>
    public CanId Id { get; }

    /// <summary>帧类型。<br/>Frame type.</summary>
    public CanFrameKind Kind { get; }

    /// <summary>帧属性标志（FD/BRS/ESI）。<br/>Frame attribute flags (FD/BRS/ESI).</summary>
    public CanFrameFlags Flags { get; }

    /// <summary>DLC 原始值（0..15）。<br/>DLC raw value (0..15).</summary>
    public byte Dlc { get; }

    /// <summary>有效负载字节数（由 DLC 映射决定）。<br/>Effective payload byte count (determined by DLC mapping).</summary>
    public int Length { get; }

    /// <summary>公开错误码或掩码（仅错误帧）。<br/>Public error code or mask (error frames only).</summary>
    public uint ErrorCode { get; }

    /// <summary>此值是否作为具体 CAN 帧创建。<br/>Whether this value was created as a concrete CAN frame.</summary>
    public bool IsInitialized => Kind != CanFrameKind.None;

    private CanFrame(CanId id, CanFrameKind kind, CanFrameFlags flags,
                     ReadOnlySpan<byte> payload, int length, byte dlc, uint errorCode)
    {
        Id = id;
        Kind = kind;
        Flags = flags;
        Length = length;
        Dlc = dlc;
        ErrorCode = errorCode;

        Span<byte> tmp = stackalloc byte[MaxPayloadLength];
        tmp.Clear();
        payload[..length].CopyTo(tmp);
        _b0 = BinaryPrimitives.ReadUInt64LittleEndian(tmp);
        _b1 = BinaryPrimitives.ReadUInt64LittleEndian(tmp[8..]);
        _b2 = BinaryPrimitives.ReadUInt64LittleEndian(tmp[16..]);
        _b3 = BinaryPrimitives.ReadUInt64LittleEndian(tmp[24..]);
        _b4 = BinaryPrimitives.ReadUInt64LittleEndian(tmp[32..]);
        _b5 = BinaryPrimitives.ReadUInt64LittleEndian(tmp[40..]);
        _b6 = BinaryPrimitives.ReadUInt64LittleEndian(tmp[48..]);
        _b7 = BinaryPrimitives.ReadUInt64LittleEndian(tmp[56..]);
    }

    /// <summary>创建 Classic CAN 数据帧。<br/>Create a Classic CAN data frame.</summary>
    public static CanFrame CreateData(CanId id, ReadOnlySpan<byte> payload, CanFrameFlags flags = CanFrameFlags.None)
    {
        int len = payload.Length;
        if (len > MaxClassicPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(payload), len,
                $"Classic CAN data frame payload cannot exceed {MaxClassicPayloadLength} bytes.");
        ValidateId(id);
        ValidateFlagsForClassic(flags);
        return new CanFrame(id, CanFrameKind.Data, flags, payload, len, (byte)len, 0);
    }

    /// <summary>创建 CAN FD 数据帧。<br/>Create a CAN FD data frame.</summary>
    public static CanFrame CreateFdData(CanId id, ReadOnlySpan<byte> payload,
        bool bitRateSwitch = true, bool errorStateIndicator = false)
    {
        int len = payload.Length;
        if (len > MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(payload), len,
                $"CAN FD data frame payload cannot exceed {MaxPayloadLength} bytes.");
        if (!IsValidFdPayloadLength(len))
            throw new ArgumentOutOfRangeException(nameof(payload), len,
                $"CAN FD payload length {len} is not DLC-representable. Valid lengths: 0..8, 12, 16, 20, 24, 32, 48, 64.");
        ValidateId(id);
        CanFrameFlags flags = CanFrameFlags.FD;
        if (bitRateSwitch) flags |= CanFrameFlags.BRS;
        if (errorStateIndicator) flags |= CanFrameFlags.ESI;
        return new CanFrame(id, CanFrameKind.Data, flags, payload, len, LengthToDlc(len), 0);
    }

    /// <summary>创建远程帧。<br/>Create a remote frame.</summary>
    public static CanFrame CreateRemote(CanId id, byte dlc = 0)
    {
        if (dlc > MaxClassicPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(dlc), dlc,
                $"Remote frame DLC cannot exceed {MaxClassicPayloadLength}.");
        ValidateId(id);
        return new CanFrame(id, CanFrameKind.Remote, CanFrameFlags.None,
            ReadOnlySpan<byte>.Empty, 0, dlc, 0);
    }

    /// <summary>创建不带错误负载的错误帧。<br/>Create an error frame with no error payload.</summary>
    public static CanFrame CreateError(uint errorCode = 0) =>
        CreateError(errorCode, ReadOnlySpan<byte>.Empty);

    /// <summary>创建带公开错误负载字节的错误帧。<br/>Create an error frame with public error payload bytes.</summary>
    public static CanFrame CreateError(uint errorCode, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxClassicPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(payload), payload.Length,
                $"Error frame payload cannot exceed {MaxClassicPayloadLength} bytes.");
        return new CanFrame(default, CanFrameKind.Error, CanFrameFlags.None,
            payload, payload.Length, (byte)payload.Length, errorCode);
    }

    /// <summary>创建过载帧。<br/>Create an overload frame.</summary>
    public static CanFrame CreateOverload() =>
        new(default, CanFrameKind.Overload, CanFrameFlags.None,
            ReadOnlySpan<byte>.Empty, 0, 0, 0);

    /// <summary>尝试创建 Classic CAN 数据帧。<br/>Try to create a Classic CAN data frame.</summary>
    public static bool TryCreateData(CanId id, ReadOnlySpan<byte> payload, out CanFrame frame) =>
        TryCreateData(id, payload, CanFrameFlags.None, out frame);

    /// <summary>尝试创建 Classic CAN 数据帧（带标志）。<br/>Try to create a Classic CAN data frame (with flags).</summary>
    public static bool TryCreateData(CanId id, ReadOnlySpan<byte> payload,
        CanFrameFlags flags, out CanFrame frame)
    {
        frame = default;
        if (payload.Length > MaxClassicPayloadLength) return false;
        if (flags != CanFrameFlags.None) return false;
        if (!IsValidId(id)) return false;
        frame = new CanFrame(id, CanFrameKind.Data, flags, payload, payload.Length, (byte)payload.Length, 0);
        return true;
    }

    /// <summary>尝试创建 CAN FD 数据帧。<br/>Try to create a CAN FD data frame.</summary>
    public static bool TryCreateFdData(CanId id, ReadOnlySpan<byte> payload,
        out CanFrame frame, bool bitRateSwitch = true, bool errorStateIndicator = false)
    {
        frame = default;
        if (payload.Length > MaxPayloadLength) return false;
        if (!IsValidFdPayloadLength(payload.Length)) return false;
        if (!IsValidId(id)) return false;
        CanFrameFlags flags = CanFrameFlags.FD;
        if (bitRateSwitch) flags |= CanFrameFlags.BRS;
        if (errorStateIndicator) flags |= CanFrameFlags.ESI;
        frame = new CanFrame(id, CanFrameKind.Data, flags, payload,
            payload.Length, LengthToDlc(payload.Length), 0);
        return true;
    }

    /// <summary>尝试创建远程帧。<br/>Try to create a remote frame.</summary>
    public static bool TryCreateRemote(CanId id, byte dlc, out CanFrame frame)
    {
        frame = default;
        if (dlc > MaxClassicPayloadLength) return false;
        if (!IsValidId(id)) return false;
        frame = new CanFrame(id, CanFrameKind.Remote, CanFrameFlags.None,
            ReadOnlySpan<byte>.Empty, 0, dlc, 0);
        return true;
    }

    /// <summary>尝试创建不带错误负载的错误帧。<br/>Try to create an error frame with no error payload.</summary>
    public static bool TryCreateError(uint errorCode, out CanFrame frame) =>
        TryCreateError(errorCode, ReadOnlySpan<byte>.Empty, out frame);

    /// <summary>尝试创建带公开错误负载字节的错误帧。<br/>Try to create an error frame with public error payload bytes.</summary>
    public static bool TryCreateError(uint errorCode, ReadOnlySpan<byte> payload, out CanFrame frame)
    {
        frame = default;
        if (payload.Length > MaxClassicPayloadLength) return false;
        frame = new CanFrame(default, CanFrameKind.Error, CanFrameFlags.None,
            payload, payload.Length, (byte)payload.Length, errorCode);
        return true;
    }

    /// <summary>尝试创建过载帧。<br/>Try to create an overload frame.</summary>
    public static bool TryCreateOverload(out CanFrame frame)
    {
        frame = new CanFrame(default, CanFrameKind.Overload, CanFrameFlags.None,
            ReadOnlySpan<byte>.Empty, 0, 0, 0);
        return true;
    }

    /// <summary>负载字节数（<see cref="Length"/> 的别名）。<br/>Payload byte count (alias for <see cref="Length"/>).</summary>
    public int PayloadLength => Length;

    /// <summary>将负载复制到目标 span。返回写入的字节数。<br/>Copy the payload into the destination span. Returns the number of bytes written.</summary>
    public void CopyPayloadTo(Span<byte> destination)
    {
        if (destination.Length < Length)
            throw new ArgumentOutOfRangeException(nameof(destination),
                $"Destination span ({destination.Length} bytes) is too small for payload ({Length} bytes).");
        ReadOnlySpan<byte> src = MemoryMarshal.AsBytes(
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _b0), 8))[..Length];
        src.CopyTo(destination);
    }

    /// <summary>尝试将负载复制到目标 span。<br/>Try to copy the payload into the destination span.</summary>
    public bool TryCopyPayloadTo(Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < Length)
        {
            bytesWritten = 0;
            return false;
        }
        ReadOnlySpan<byte> src = MemoryMarshal.AsBytes(
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _b0), 8))[..Length];
        src.CopyTo(destination);
        bytesWritten = Length;
        return true;
    }

    /// <summary>按索引获取单个负载字节。索引越界则抛出异常。<br/>Get a single payload byte by index. Throws if index is out of range.</summary>
    public byte GetPayloadByte(int index)
    {
        if ((uint)index >= (uint)Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of range for payload of length {Length}.");
        ReadOnlySpan<byte> src = MemoryMarshal.AsBytes(
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _b0), 8))[..Length];
        return src[index];
    }

    /// <summary>负载为空。<br/>Payload is empty.</summary>
    public bool IsEmpty => Length == 0;

    /// <summary>是否为可提交到发送路径的帧类型。<br/>Whether this frame kind can be submitted to the transmit path.</summary>
    public bool IsTransmittable => Kind is CanFrameKind.Data or CanFrameKind.Remote;

    /// <summary>DLC 映射：0..8 直接对应，9->12, 10->16, 11->20, 12->24, 13->32, 14->48, 15->64。<br/>DLC mapping: 0..8 direct, 9->12, 10->16, 11->20, 12->24, 13->32, 14->48, 15->64.</summary>
    public static int DlcToLength(byte dlc) => dlc switch
    {
        <= 8 => dlc,
        9 => 12,
        10 => 16,
        11 => 20,
        12 => 24,
        13 => 32,
        14 => 48,
        15 => 64,
        _ => throw new ArgumentOutOfRangeException(nameof(dlc), dlc, "DLC must be in 0..15 range.")
    };

    /// <summary>将 DLC 可表示的负载长度转换为 DLC。<br/>Convert a DLC-representable payload length to DLC.</summary>
    public static byte LengthToDlc(int length) => length switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(length), length,
            "Payload length must not be negative."),
        <= 8 => (byte)length,
        12 => 9,
        16 => 10,
        20 => 11,
        24 => 12,
        32 => 13,
        48 => 14,
        64 => 15,
        <= 64 => throw new ArgumentOutOfRangeException(nameof(length), length,
            "CAN FD payload length must be DLC-representable: 0..8, 12, 16, 20, 24, 32, 48, or 64 bytes."),
        _ => throw new ArgumentOutOfRangeException(nameof(length), length,
            "Payload length must not exceed 64 bytes.")
    };

    /// <summary>检查负载长度对 Classic 或 FD 是否有效。<br/>Check whether a payload length is valid for Classic or FD.</summary>
    public static bool IsValidPayloadLength(int length, bool allowFd) =>
        allowFd
            ? IsValidFdPayloadLength(length)
            : length >= 0 && length <= MaxClassicPayloadLength;

    /// <summary>检查负载长度是否为有效的 CAN FD DLC 可表示长度（0..8, 12, 16, 20, 24, 32, 48, 64）。<br/>Check whether a payload length is a valid CAN FD DLC-representable length (0..8, 12, 16, 20, 24, 32, 48, 64).</summary>
    public static bool IsValidFdPayloadLength(int length) =>
        length is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 12 or 16 or 20 or 24 or 32 or 48 or 64;

    /// <summary>尝试将负载长度转换为 DLC。对非 DLC 可表示的长度返回 false。<br/>Try to convert payload length to DLC. Returns false for non-DLC-representable lengths.</summary>
    public static bool TryLengthToDlc(int length, out byte dlc)
    {
        if (length is >= 0 and <= 8) { dlc = (byte)length; return true; }
        dlc = length switch
        {
            12 => 9,
            16 => 10,
            20 => 11,
            24 => 12,
            32 => 13,
            48 => 14,
            64 => 15,
            _ => 0
        };
        return dlc != 0 || length == 0;
    }

    /// <summary>检查 DLC 值是否在有效范围内。<br/>Check whether a DLC value is in valid range.</summary>
    public static bool IsValidDlc(byte dlc) => dlc <= 15;

    /// <summary>判断两个 CAN 帧是否相等（值比较）。<br/>Determines whether two CAN frames are equal (value comparison).</summary>
    public bool Equals(CanFrame other)
    {
        if (Id.Value != other.Id.Value || Id.IsExtended != other.Id.IsExtended ||
            Kind != other.Kind || Flags != other.Flags || Dlc != other.Dlc ||
            ErrorCode != other.ErrorCode || Length != other.Length)
            return false;

        return _b0 == other._b0 && _b1 == other._b1 && _b2 == other._b2 && _b3 == other._b3 &&
               _b4 == other._b4 && _b5 == other._b5 && _b6 == other._b6 && _b7 == other._b7;
    }

    /// <inheritdoc cref="Equals(CanFrame)"/>
    public override bool Equals(object? obj) => obj is CanFrame other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id.Value);
        hash.Add(Id.IsExtended);
        hash.Add(Kind);
        hash.Add(Flags);
        hash.Add(Dlc);
        hash.Add(ErrorCode);
        hash.Add(Length);
        hash.Add(_b0);
        hash.Add(_b1);
        hash.Add(_b2);
        hash.Add(_b3);
        hash.Add(_b4);
        hash.Add(_b5);
        hash.Add(_b6);
        hash.Add(_b7);
        return hash.ToHashCode();
    }

    /// <summary>判断两个 CAN 帧是否相等。<br/>Determines whether two CAN frames are equal.</summary>
    public static bool operator ==(CanFrame left, CanFrame right) => left.Equals(right);

    /// <summary>判断两个 CAN 帧是否不等。<br/>Determines whether two CAN frames are not equal.</summary>
    public static bool operator !=(CanFrame left, CanFrame right) => !left.Equals(right);

    /// <summary>返回 CAN 帧的字符串表示。<br/>Returns a string representation of the CAN frame.</summary>
    public override string ToString()
    {
        if (!IsInitialized) return "CanFrame[uninitialized]";

        var idStr = Id.IsExtended ? $"{Id.Value:X8}" : $"{Id.Value:X3}";
        var kindStr = Kind switch
        {
            CanFrameKind.Data => Flags.HasFlag(CanFrameFlags.FD) ? "FD" : "Data",
            CanFrameKind.Remote => "RTR",
            CanFrameKind.Error => "ERR",
            CanFrameKind.Overload => "OVL",
            _ => Kind.ToString()
        };

        int previewLen = Math.Min(Length, 8);
        Span<byte> payload = stackalloc byte[Length];
        CopyPayloadTo(payload);

        Span<char> hex = stackalloc char[previewLen * 3 + 2]; // +2 for ".."
        int pos = 0;
        for (int i = 0; i < previewLen; i++)
        {
            if (i > 0) hex[pos++] = ' ';
            payload[i].TryFormat(hex[pos..], out int w, "X2");
            pos += w;
        }
        if (Length > 8) { hex[pos++] = '.'; hex[pos++] = '.'; }

        return $"CanFrame[{kindStr}] ID=0x{idStr} DLC={Dlc} [{new string(hex[..pos])}]";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateId(CanId id)
    {
        if (!IsValidId(id))
            throw new ArgumentOutOfRangeException(nameof(id),
                $"Invalid CAN ID: 0x{id.Value:X}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateFlagsForClassic(CanFrameFlags flags)
    {
        if (flags != CanFrameFlags.None)
            throw new ArgumentException("Classic CAN frame must not have FD/BRS/ESI flags.", nameof(flags));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidId(CanId id) =>
        id.IsExtended ? id.Value <= CanId.MaxExtended : id.Value <= CanId.MaxStandard;
}
