using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 原生 API 返回状态码。<br/>
/// ZLG native API return status codes.
/// </summary>
public enum ZlgStatus : uint
{
    /// <summary>操作失败。<br/>Operation failed.</summary>
    Error = 0,
    /// <summary>操作成功。<br/>Operation succeeded.</summary>
    Ok = 1,
    /// <summary>设备在线。<br/>Device is online.</summary>
    Online = 2,
    /// <summary>设备离线。<br/>Device is offline.</summary>
    Offline = 3,
    /// <summary>不支持的操作。<br/>Unsupported operation.</summary>
    Unsupported = 4,
    /// <summary>缓冲区太小。<br/>Buffer too small.</summary>
    BufferTooSmall = 5,
}

/// <summary>
/// ZLG 设备类型标识。对应 ZCAN_OpenDevice 的 deviceType 参数。<br/>
/// ZLG device type identifiers. Corresponds to the deviceType parameter of ZCAN_OpenDevice.
/// </summary>
public enum ZlgDeviceType : uint
{
    /// <summary>USBCANFD-200U 设备。<br/>USBCANFD-200U device.</summary>
    UsbCanFd200U = 41,
}

/// <summary>
/// ZLG CAN 总线类型。<br/>
/// ZLG CAN bus type.
/// </summary>
public enum ZlgCanBusType : uint
{
    /// <summary>经典 CAN。<br/>Classic CAN.</summary>
    ClassicCan = 0,
    /// <summary>CAN FD。<br/>CAN FD.</summary>
    CanFd = 1,
}

/// <summary>
/// ZLG 接收缓冲区类型。<br/>
/// ZLG receive buffer type.
/// </summary>
public enum ZlgReceiveBufferType : byte
{
    /// <summary>经典 CAN 缓冲区。<br/>Classic CAN buffer.</summary>
    ClassicCan = 0,
    /// <summary>CAN FD 缓冲区。<br/>CAN FD buffer.</summary>
    CanFd = 1,
    /// <summary>合并数据缓冲区。<br/>All data (merged) buffer.</summary>
    AllData = 2,
}

/// <summary>
/// ZLG 数据对象类型。标识 NativeDataObject 中携带的数据类型。<br/>
/// ZLG data object type. Identifies the data type carried within a NativeDataObject.
/// </summary>
public enum ZlgDataObjectType : byte
{
    /// <summary>CAN 或 CAN FD 帧。<br/>CAN or CAN FD frame.</summary>
    CanOrCanFd = 1,
    /// <summary>错误信息。<br/>Error information.</summary>
    Error = 2,
    /// <summary>GPS 数据。<br/>GPS data.</summary>
    Gps = 3,
    /// <summary>LIN 数据。<br/>LIN data.</summary>
    Lin = 4,
    /// <summary>总线使用率。<br/>Bus usage statistics.</summary>
    BusUsage = 5,
    /// <summary>LIN 错误。<br/>LIN error.</summary>
    LinError = 6,
}

/// <summary>
/// ZLG 错误类型。<br/>
/// ZLG error type.
/// </summary>
public enum ZlgErrorType : byte
{
    /// <summary>未知错误。<br/>Unknown error.</summary>
    Unknown = 0,
    /// <summary>总线错误。<br/>Bus error.</summary>
    BusError = 1,
    /// <summary>控制器错误。<br/>Controller error.</summary>
    ControllerError = 2,
    /// <summary>设备错误。<br/>Device error.</summary>
    DeviceError = 3,
}

/// <summary>
/// ZLG 总线错误子类型。<br/>
/// ZLG bus error subtype.
/// </summary>
public enum ZlgBusErrorSubType : byte
{
    /// <summary>无错误。<br/>No error.</summary>
    None = 0,
    /// <summary>位错误。<br/>Bit error.</summary>
    BitError = 1,
    /// <summary>ACK 错误。<br/>ACK error.</summary>
    AckError = 2,
    /// <summary>CRC 错误。<br/>CRC error.</summary>
    CrcError = 3,
    /// <summary>格式错误。<br/>Form error.</summary>
    FormError = 4,
    /// <summary>填充错误。<br/>Stuff error.</summary>
    StuffError = 5,
    /// <summary>过载错误。<br/>Overload error.</summary>
    OverloadError = 6,
    /// <summary>仲裁丢失。<br/>Arbitration lost.</summary>
    ArbitrationLost = 7,
    /// <summary>节点状态变化。<br/>Node state change.</summary>
    NodeStateChange = 8,
}

/// <summary>
/// ZLG CAN 节点状态。<br/>
/// ZLG CAN node state.
/// </summary>
public enum ZlgNodeState : byte
{
    /// <summary>未知状态。<br/>Unknown state.</summary>
    Unknown = 0,
    /// <summary>主动错误模式。<br/>Error-active mode.</summary>
    Active = 1,
    /// <summary>警告状态。<br/>Warning state.</summary>
    Warning = 2,
    /// <summary>被动错误模式。<br/>Error-passive mode.</summary>
    Passive = 3,
    /// <summary>总线关闭。<br/>Bus-off.</summary>
    BusOff = 4,
}

/// <summary>
/// ZLG CAN ID 标志位。对应原生 API 中 CanId 字段的高位标志。<br/>
/// ZLG CAN ID flags. Corresponds to the high-order bits of the CanId field in the native API.
/// </summary>
[Flags]
public enum ZlgCanIdFlags : uint
{
    /// <summary>扩展帧标志。<br/>Extended frame flag.</summary>
    Extended = 0x80000000,
    /// <summary>远程帧标志。<br/>Remote frame flag.</summary>
    Remote = 0x40000000,
    /// <summary>错误帧标志。<br/>Error frame flag.</summary>
    Error = 0x20000000,
    /// <summary>ID 掩码。<br/>ID mask.</summary>
    IdMask = 0x1FFFFFFF,
}

/// <summary>
/// ZLG 经典 CAN 帧标志位。<br/>
/// ZLG classic CAN frame flags.
/// </summary>
[Flags]
public enum ZlgCanFrameFlags : byte
{
    /// <summary>无标志。<br/>No flags.</summary>
    None = 0,
    /// <summary>发送回显。<br/>Transmit echo.</summary>
    TxEcho = 0x20,
    /// <summary>队列时间单位为 100us。<br/>Queue time unit is 100us.</summary>
    QueueTimeUnit100Us = 0x40,
    /// <summary>队列已启用。<br/>Queue enabled.</summary>
    QueueEnabled = 0x80,
}

/// <summary>
/// ZLG CAN FD 帧标志位。<br/>
/// ZLG CAN FD frame flags.
/// </summary>
[Flags]
public enum ZlgCanFdFrameFlags : byte
{
    /// <summary>无标志。<br/>No flags.</summary>
    None = 0,
    /// <summary>位速率切换。<br/>Bit rate switch.</summary>
    BitRateSwitch = 0x01,
    /// <summary>错误状态指示。<br/>Error state indicator.</summary>
    ErrorStateIndicator = 0x02,
    /// <summary>发送回显。<br/>Transmit echo.</summary>
    TxEcho = 0x20,
    /// <summary>队列时间单位为 100us。<br/>Queue time unit is 100us.</summary>
    QueueTimeUnit100Us = 0x40,
    /// <summary>队列已启用。<br/>Queue enabled.</summary>
    QueueEnabled = 0x80,
}

/// <summary>
/// 原生经典 CAN 帧结构。对应 ZLG 驱动的 _ZCAN_FRAME 结构。<br/>
/// Native classic CAN frame struct. Corresponds to the _ZCAN_FRAME structure in the ZLG driver.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NativeCanFrame
{
    /// <summary>CAN ID（含扩展帧、远程帧标志位）。<br/>CAN ID (includes extended/remote frame flags).</summary>
    public uint CanId;
    /// <summary>数据长度。<br/>Data length.</summary>
    public byte Length;
    /// <summary>帧标志位。<br/>Frame flags.</summary>
    public byte Flags;
    /// <summary>保留字段。<br/>Reserved field.</summary>
    public byte Reserved0;
    /// <summary>保留字段。<br/>Reserved field.</summary>
    public byte Reserved1;
    /// <summary>数据字节（内联 8 字节）。<br/>Data bytes (inline 8 bytes).</summary>
    public fixed byte Data[8];

    /// <summary>
    /// 将数据复制到内联 8 字节缓冲区中。<br/>
    /// Copies data into the inline 8-byte buffer.
    /// </summary>
    public void SetData(ReadOnlySpan<byte> data)
    {
        if (data.Length > 8)
            throw new ArgumentOutOfRangeException(nameof(data), "Classic CAN data length must be <= 8.");

        Length = (byte)data.Length;
        fixed (byte* dataPointer = Data)
        {
            var target = new Span<byte>(dataPointer, 8);
            target.Clear();
            data.CopyTo(target);
        }
    }

    /// <summary>
    /// 有效数据长度，上限为 8 字节。<br/>
    /// Effective data length, capped at 8 bytes.
    /// </summary>
    public readonly int DataLength => Math.Min(Length, (byte)8);

    /// <summary>
    /// 将数据复制到目标缓冲区中。<br/>
    /// Copies data into the destination buffer.
    /// </summary>
    public readonly void CopyDataTo(Span<byte> destination)
    {
        var length = DataLength;
        if (destination.Length < length)
            throw new ArgumentOutOfRangeException(nameof(destination), destination.Length, "Destination is too small for native classic CAN data.");

        fixed (byte* dataPointer = Data)
        {
            new ReadOnlySpan<byte>(dataPointer, length).CopyTo(destination);
        }
    }

    /// <summary>
    /// 以托管数组形式获取数据。<br/>
    /// Gets the data as a managed byte array.
    /// </summary>
    public readonly byte[] GetData()
    {
        var data = new byte[DataLength];
        CopyDataTo(data);
        return data;
    }
}

/// <summary>
/// 原生 CAN FD 帧结构。对应 ZLG 驱动的 _ZCAN_FD_FRAME 结构。<br/>
/// Native CAN FD frame struct. Corresponds to the _ZCAN_FD_FRAME structure in the ZLG driver.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NativeCanFdFrame
{
    /// <summary>CAN ID（含扩展帧、远程帧标志位）。<br/>CAN ID (includes extended/remote frame flags).</summary>
    public uint CanId;
    /// <summary>数据长度。<br/>Data length.</summary>
    public byte Length;
    /// <summary>帧标志位（BRS、ESI 等）。<br/>Frame flags (BRS, ESI, etc.).</summary>
    public byte Flags;
    /// <summary>保留字段。<br/>Reserved field.</summary>
    public byte Reserved0;
    /// <summary>保留字段。<br/>Reserved field.</summary>
    public byte Reserved1;
    /// <summary>数据字节（内联 64 字节）。<br/>Data bytes (inline 64 bytes).</summary>
    public fixed byte Data[64];

    /// <summary>
    /// 将数据复制到内联 64 字节缓冲区中。<br/>
    /// Copies data into the inline 64-byte buffer.
    /// </summary>
    public void SetData(ReadOnlySpan<byte> data)
    {
        if (data.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(data), "CAN FD data length must be <= 64.");

        Length = (byte)data.Length;
        fixed (byte* dataPointer = Data)
        {
            var target = new Span<byte>(dataPointer, 64);
            target.Clear();
            data.CopyTo(target);
        }
    }

    /// <summary>
    /// 有效数据长度，上限为 64 字节。<br/>
    /// Effective data length, capped at 64 bytes.
    /// </summary>
    public readonly int DataLength => Math.Min(Length, (byte)64);

    /// <summary>
    /// 将数据复制到目标缓冲区中。<br/>
    /// Copies data into the destination buffer.
    /// </summary>
    public readonly void CopyDataTo(Span<byte> destination)
    {
        var length = DataLength;
        if (destination.Length < length)
            throw new ArgumentOutOfRangeException(nameof(destination), destination.Length, "Destination is too small for native CAN FD data.");

        fixed (byte* dataPointer = Data)
        {
            new ReadOnlySpan<byte>(dataPointer, length).CopyTo(destination);
        }
    }

    /// <summary>
    /// 以托管数组形式获取数据。<br/>
    /// Gets the data as a managed byte array.
    /// </summary>
    public readonly byte[] GetData()
    {
        var data = new byte[DataLength];
        CopyDataTo(data);
        return data;
    }
}

/// <summary>
/// 原生经典 CAN 发送数据。对应 ZCAN_Transmit 的帧+发送类型组合。<br/>
/// Native classic CAN transmit data. Combines a frame with a transmit type for ZCAN_Transmit.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeTransmitData
{
    /// <summary>经典 CAN 帧。<br/>Classic CAN frame.</summary>
    public NativeCanFrame Frame;
    /// <summary>发送类型。<br/>Transmit type.</summary>
    public uint TransmitType;
}

/// <summary>
/// 原生 CAN FD 发送数据。对应 ZCAN_TransmitFD 的帧+发送类型组合。<br/>
/// Native CAN FD transmit data. Combines a frame with a transmit type for ZCAN_TransmitFD.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeTransmitFdData
{
    /// <summary>CAN FD 帧。<br/>CAN FD frame.</summary>
    public NativeCanFdFrame Frame;
    /// <summary>发送类型。<br/>Transmit type.</summary>
    public uint TransmitType;
}

/// <summary>
/// 原生经典 CAN 接收数据。包含帧和时间戳。<br/>
/// Native classic CAN receive data. Contains a frame and a timestamp.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeReceiveData
{
    /// <summary>经典 CAN 帧。<br/>Classic CAN frame.</summary>
    public NativeCanFrame Frame;
    /// <summary>时间戳（微秒）。<br/>Timestamp (microseconds).</summary>
    public ulong Timestamp;
}

/// <summary>
/// 原生 CAN FD 接收数据。包含帧和时间戳。<br/>
/// Native CAN FD receive data. Contains a frame and a timestamp.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeReceiveFdData
{
    /// <summary>CAN FD 帧。<br/>CAN FD frame.</summary>
    public NativeCanFdFrame Frame;
    /// <summary>时间戳（微秒）。<br/>Timestamp (microseconds).</summary>
    public ulong Timestamp;
}

/// <summary>
/// 原生 CAN FD 数据（合并接收模式）。包含时间戳、标志位和帧数据。<br/>
/// Native CAN FD data (merged receive mode). Contains a timestamp, flags, and frame data.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NativeCanFdData
{
    /// <summary>时间戳（微秒）。<br/>Timestamp (microseconds).</summary>
    public ulong Timestamp;
    /// <summary>标志位：bit[0]=1 表示 FD 帧，bit[9]=1 表示发送回显。<br/>Flag: bit[0]=1 indicates FD frame, bit[9]=1 indicates TX echo.</summary>
    public uint Flag;
    /// <summary>额外数据。<br/>Extra data.</summary>
    public fixed byte ExtraData[4];
    /// <summary>CAN FD 帧。<br/>CAN FD frame.</summary>
    public NativeCanFdFrame Frame;
}

/// <summary>
/// 原生错误数据。包含错误类型、节点状态和收发错误计数器。<br/>
/// Native error data. Contains error type, node state, and RX/TX error counters.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NativeErrorData
{
    /// <summary>时间戳（微秒）。<br/>Timestamp (microseconds).</summary>
    public ulong Timestamp;
    /// <summary>错误类型。<br/>Error type.</summary>
    public byte ErrorType;
    /// <summary>错误子类型。<br/>Error subtype.</summary>
    public byte ErrorSubType;
    /// <summary>节点状态。<br/>Node state.</summary>
    public byte NodeState;
    /// <summary>接收错误计数。<br/>Receive error counter.</summary>
    public byte RxErrorCount;
    /// <summary>发送错误计数。<br/>Transmit error counter.</summary>
    public byte TxErrorCount;
    /// <summary>错误数据。<br/>Error data.</summary>
    public byte ErrorData;
    /// <summary>保留字段。<br/>Reserved fields.</summary>
    public fixed byte Reserved[2];
}

/// <summary>
/// 原生数据对象（合并接收模式）。128 字节的联合体，携带 CAN/错误/GPS/LIN 等多种数据类型。<br/>
/// Native data object (merged receive mode). A 128-byte union carrying various data types: CAN, error, GPS, LIN, etc.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NativeDataObject
{
    /// <summary>数据类型。<br/>Data type.</summary>
    public byte DataType;
    /// <summary>通道索引。<br/>Channel index.</summary>
    public byte Channel;
    /// <summary>标志位。<br/>Flags.</summary>
    public ushort Flag;
    /// <summary>额外数据。<br/>Extra data.</summary>
    public fixed byte ExtraData[4];
    /// <summary>原始数据（92 字节联合体）。<br/>Raw data (92-byte union).</summary>
    public fixed byte Raw[92];

    /// <summary>
    /// 从原始联合体中读取 CAN FD 数据。<br/>
    /// Reads CAN FD data from the raw union.
    /// </summary>
    public readonly NativeCanFdData ReadCanFdData()
    {
        fixed (byte* rawPointer = Raw)
        {
            return Unsafe.ReadUnaligned<NativeCanFdData>(rawPointer);
        }
    }

    /// <summary>
    /// 将 CAN FD 数据写入原始联合体。<br/>
    /// Writes CAN FD data into the raw union.
    /// </summary>
    public void WriteCanFdData(in NativeCanFdData data)
    {
        fixed (byte* rawPointer = Raw)
        {
            Unsafe.WriteUnaligned(rawPointer, data);
        }
    }

    /// <summary>
    /// 从原始联合体中读取错误数据。<br/>
    /// Reads error data from the raw union.
    /// </summary>
    public readonly NativeErrorData ReadErrorData()
    {
        fixed (byte* rawPointer = Raw)
        {
            return Unsafe.ReadUnaligned<NativeErrorData>(rawPointer);
        }
    }
}

/// <summary>
/// 原生设备信息结构。对应 ZCAN_GetDeviceInf 返回的设备信息。<br/>
/// Native device info struct. Corresponds to the device information returned by ZCAN_GetDeviceInf.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct NativeDeviceInfo
{
    /// <summary>硬件版本号。<br/>Hardware version.</summary>
    public ushort HardwareVersion;
    /// <summary>固件版本号。<br/>Firmware version.</summary>
    public ushort FirmwareVersion;
    /// <summary>驱动版本号。<br/>Driver version.</summary>
    public ushort DriverVersion;
    /// <summary>接口版本号。<br/>Interface version.</summary>
    public ushort InterfaceVersion;
    /// <summary>IRQ 号。<br/>IRQ number.</summary>
    public ushort IrqNumber;
    /// <summary>CAN 通道数量。<br/>CAN channel count.</summary>
    public byte CanChannelCount;
    /// <summary>序列号（最多 20 字节 ASCII）。<br/>Serial number (up to 20 bytes ASCII).</summary>
    public fixed byte SerialNumber[20];
    /// <summary>硬件类型（最多 40 字节 ASCII）。<br/>Hardware type (up to 40 bytes ASCII).</summary>
    public fixed byte HardwareType[40];
    /// <summary>保留字段。<br/>Reserved fields.</summary>
    public fixed ushort Reserved[4];

    /// <summary>
    /// 将原生设备信息转换为托管 <see cref="ZlgDeviceInfo"/> 对象。<br/>
    /// Converts the native device info into a managed <see cref="ZlgDeviceInfo"/> object.
    /// </summary>
    public readonly ZlgDeviceInfo ToManaged(uint deviceIndex, ZlgDeviceType requestedType)
    {
        fixed (byte* serialPointer = SerialNumber)
        fixed (byte* hardwarePointer = HardwareType)
        {
            return new ZlgDeviceInfo(
                requestedType,
                deviceIndex,
                FormatVersion(HardwareVersion),
                FormatVersion(FirmwareVersion),
                FormatVersion(DriverVersion),
                FormatVersion(InterfaceVersion),
                IrqNumber,
                CanChannelCount,
                ReadNullTerminatedAscii(serialPointer, 20),
                ReadNullTerminatedAscii(hardwarePointer, 40));
        }
    }

    private static string FormatVersion(ushort version) => $"{version >> 8}.{version & 0xFF}";

    private static unsafe string ReadNullTerminatedAscii(byte* pointer, int length)
    {
        var span = new ReadOnlySpan<byte>(pointer, length);
        var terminator = span.IndexOf((byte)0);
        if (terminator >= 0)
            span = span[..terminator];
        return Encoding.ASCII.GetString(span).Trim();
    }
}

/// <summary>
/// 原生经典 CAN 初始化配置。对应 ZCAN_InitCAN 的 CAN 初始化参数。<br/>
/// Native classic CAN init config. Corresponds to the CAN init parameters for ZCAN_InitCAN.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeCanInitConfig
{
    /// <summary>验收码。<br/>Acceptance code.</summary>
    public uint AccCode;
    /// <summary>验收屏蔽码。<br/>Acceptance mask.</summary>
    public uint AccMask;
    /// <summary>保留字段。<br/>Reserved field.</summary>
    public uint Reserved;
    /// <summary>滤波模式。<br/>Filter mode.</summary>
    public byte Filter;
    /// <summary>时序 0 寄存器。<br/>Timing 0 register.</summary>
    public byte Timing0;
    /// <summary>时序 1 寄存器。<br/>Timing 1 register.</summary>
    public byte Timing1;
    /// <summary>工作模式。<br/>Work mode.</summary>
    public byte Mode;
}

/// <summary>
/// 原生 CAN FD 初始化配置。对应 ZCAN_InitCAN 的 CAN FD 初始化参数。<br/>
/// Native CAN FD init config. Corresponds to the CAN FD init parameters for ZCAN_InitCAN.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeCanFdInitConfig
{
    /// <summary>验收码。<br/>Acceptance code.</summary>
    public uint AccCode;
    /// <summary>验收屏蔽码。<br/>Acceptance mask.</summary>
    public uint AccMask;
    /// <summary>仲裁段时序。<br/>Arbitration timing.</summary>
    public uint ArbitrationTiming;
    /// <summary>数据段时序。<br/>Data timing.</summary>
    public uint DataTiming;
    /// <summary>预分频器。<br/>Prescaler.</summary>
    public uint Prescaler;
    /// <summary>滤波模式。<br/>Filter mode.</summary>
    public byte Filter;
    /// <summary>工作模式。<br/>Work mode.</summary>
    public byte Mode;
    /// <summary>对齐填充。<br/>Padding for alignment.</summary>
    public ushort Pad;
    /// <summary>保留字段。<br/>Reserved field.</summary>
    public uint Reserved;
}

/// <summary>
/// 原生通道初始化联合体。允许使用 CAN 或 CAN FD 初始化配置覆盖同一内存区域。<br/>
/// Native channel init union. Allows overlapping CAN or CAN FD init config at the same memory location.
/// </summary>
[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct NativeChannelInitUnion
{
    /// <summary>经典 CAN 初始化配置。<br/>Classic CAN init config.</summary>
    [FieldOffset(0)]
    public NativeCanInitConfig Can;

    /// <summary>CAN FD 初始化配置。<br/>CAN FD init config.</summary>
    [FieldOffset(0)]
    public NativeCanFdInitConfig CanFd;
}

/// <summary>
/// 原生通道初始化配置。包含总线类型选择及对应的 CAN/CAN FD 配置联合体。<br/>
/// Native channel init config. Contains bus type selection and the corresponding CAN/CAN FD config union.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeChannelInitConfig
{
    /// <summary>CAN 总线类型。<br/>CAN bus type.</summary>
    public uint CanType;
    /// <summary>初始化配置联合体。<br/>Init config union.</summary>
    public NativeChannelInitUnion Config;

    /// <summary>
    /// 创建 CAN FD 类型的通道初始化配置。<br/>
    /// Creates a CAN FD type channel init configuration.
    /// </summary>
    public static NativeChannelInitConfig CreateCanFd(
        ZlgWorkMode workMode,
        uint accCode = 0,
        uint accMask = 0xFFFFFFFF)
    {
        return new NativeChannelInitConfig
        {
            CanType = (uint)ZlgCanBusType.CanFd,
            Config = new NativeChannelInitUnion
            {
                CanFd = new NativeCanFdInitConfig
                {
                    AccCode = accCode,
                    AccMask = accMask,
                    Filter = 1,
                    Mode = (byte)workMode,
                },
            },
        };
    }
}

/// <summary>
/// ZLG 设备托管信息记录。从原生 NativeDeviceInfo 转换而来的类型安全表示。<br/>
/// Managed ZLG device info record. Type-safe representation converted from the native NativeDeviceInfo.
/// </summary>
/// <param name="DeviceType">设备类型。<br/>Device type.</param>
/// <param name="DeviceIndex">设备索引。<br/>Device index.</param>
/// <param name="HardwareVersion">硬件版本。<br/>Hardware version.</param>
/// <param name="FirmwareVersion">固件版本。<br/>Firmware version.</param>
/// <param name="DriverVersion">驱动版本。<br/>Driver version.</param>
/// <param name="InterfaceVersion">接口版本。<br/>Interface version.</param>
/// <param name="IrqNumber">IRQ 号。<br/>IRQ number.</param>
/// <param name="CanChannelCount">CAN 通道数量。<br/>CAN channel count.</param>
/// <param name="SerialNumber">序列号。<br/>Serial number.</param>
/// <param name="HardwareType">硬件类型。<br/>Hardware type.</param>
public sealed record ZlgDeviceInfo(
    ZlgDeviceType DeviceType,
    uint DeviceIndex,
    string HardwareVersion,
    string FirmwareVersion,
    string DriverVersion,
    string InterfaceVersion,
    ushort IrqNumber,
    byte CanChannelCount,
    string SerialNumber,
    string HardwareType);
