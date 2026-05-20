using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 原生 API 封装。通过 P/Invoke 调用 zlgcan.dll，提供托管包装方法和错误处理。<br/>
/// ZLG native API wrapper. Provides managed wrappers around zlgcan.dll P/Invoke calls with error handling.
/// </summary>
public static partial class ZlgNative
{
    private const string DllName = "zlgcan.dll";

    static ZlgNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(ZlgNative).Assembly, ResolveImport);
    }

    /// <summary>
    /// 打开 ZLG 设备。失败时抛出 ZlgApiException。<br/>
    /// Opens a ZLG device. Throws ZlgApiException on failure.
    /// </summary>
    public static nint OpenDevice(ZlgDeviceType deviceType, uint deviceIndex)
    {
        var handle = ZCAN_OpenDevice((uint)deviceType, deviceIndex, 0);
        if (handle == 0)
            throw new ZlgApiException("ZCAN_OpenDevice", ZlgStatus.Error, $"deviceType={(uint)deviceType}, deviceIndex={deviceIndex}");
        return handle;
    }

    /// <summary>
    /// 尝试打开 ZLG 设备。成功返回 true，失败时返回 false（不抛出异常）。<br/>
    /// Attempts to open a ZLG device. Returns true on success, false on failure (no exception thrown).
    /// </summary>
    public static bool TryOpenDevice(ZlgDeviceType deviceType, uint deviceIndex, out nint handle)
    {
        handle = ZCAN_OpenDevice((uint)deviceType, deviceIndex, 0);
        return handle != 0;
    }

    /// <summary>关闭 ZLG 设备句柄。<br/>Closes a ZLG device handle.</summary>
    public static ZlgStatus CloseDevice(nint deviceHandle) =>
        (ZlgStatus)ZCAN_CloseDevice(deviceHandle);

    /// <summary>查询设备是否在线。<br/>Queries whether the device is online.</summary>
    public static ZlgStatus IsDeviceOnline(nint deviceHandle) =>
        (ZlgStatus)ZCAN_IsDeviceOnLine(deviceHandle);

    /// <summary>获取设备信息。失败时抛出 ZlgApiException。<br/>Gets device information. Throws ZlgApiException on failure.</summary>
    public static unsafe ZlgDeviceInfo GetDeviceInfo(nint deviceHandle, uint deviceIndex, ZlgDeviceType requestedType)
    {
        var nativeInfo = new NativeDeviceInfo();
        var status = (ZlgStatus)ZCAN_GetDeviceInf(deviceHandle, &nativeInfo);
        ThrowIfNotOk(status, "ZCAN_GetDeviceInf");
        return nativeInfo.ToManaged(deviceIndex, requestedType);
    }

    /// <summary>设置设备参数。<br/>Sets a device parameter.</summary>
    public static ZlgStatus SetValue(nint deviceHandle, string path, string value) =>
        (ZlgStatus)ZCAN_SetValue(deviceHandle, path, value);

    /// <summary>获取设备参数值。<br/>Gets a device parameter value.</summary>
    public static string? GetValue(nint deviceHandle, string path)
    {
        var valuePointer = ZCAN_GetValue(deviceHandle, path);
        return valuePointer == 0 ? null : Marshal.PtrToStringAnsi(valuePointer);
    }

    /// <summary>设置仲裁段波特率。<br/>Sets the arbitration bitrate.</summary>
    public static ZlgStatus SetArbitrationBitrate(nint deviceHandle, uint channelIndex, uint bitrate) =>
        SetValue(deviceHandle, $"{channelIndex}/canfd_abit_baud_rate", bitrate.ToString());

    /// <summary>设置数据段波特率。<br/>Sets the data bitrate.</summary>
    public static ZlgStatus SetDataBitrate(nint deviceHandle, uint channelIndex, uint bitrate) =>
        SetValue(deviceHandle, $"{channelIndex}/canfd_dbit_baud_rate", bitrate.ToString());

    /// <summary>设置 CAN FD 标准（ISO 或 Non-ISO）。<br/>Sets the CAN FD standard (ISO or Non-ISO).</summary>
    public static ZlgStatus SetCanFdStandard(nint deviceHandle, uint channelIndex, bool nonIsoFd) =>
        SetValue(deviceHandle, $"{channelIndex}/canfd_standard", nonIsoFd ? "1" : "0");

    /// <summary>设置发送超时时间。<br/>Sets the transmit timeout.</summary>
    public static ZlgStatus SetTxTimeout(nint deviceHandle, uint channelIndex, TimeSpan timeout) =>
        SetValue(deviceHandle, $"{channelIndex}/tx_timeout", ((uint)timeout.TotalMilliseconds).ToString());

    /// <summary>设置内部终端电阻。<br/>Sets the internal termination resistance.</summary>
    public static ZlgStatus SetInternalResistance(nint deviceHandle, uint channelIndex, bool enabled) =>
        SetValue(deviceHandle, $"{channelIndex}/initenal_resistance", enabled ? "1" : "0");

    /// <summary>设置设备级合并接收模式。<br/>Sets the device-level merged receive mode.</summary>
    public static ZlgStatus SetDeviceReceiveMerge(nint deviceHandle, uint channelIndex, bool enabled) =>
        SetValue(deviceHandle, $"{channelIndex}/set_device_recv_merge", enabled ? "1" : "0");

    /// <summary>初始化 CAN 通道。失败时抛出 ZlgApiException。<br/>Initializes a CAN channel. Throws ZlgApiException on failure.</summary>
    public static unsafe nint InitCan(nint deviceHandle, uint channelIndex, in NativeChannelInitConfig initConfig)
    {
        var config = initConfig;
        var channelHandle = ZCAN_InitCAN(deviceHandle, channelIndex, &config);
        if (channelHandle == 0)
            throw new ZlgApiException("ZCAN_InitCAN", ZlgStatus.Error, $"channelIndex={channelIndex}");
        return channelHandle;
    }

    /// <summary>启动 CAN 通道。<br/>Starts the CAN channel.</summary>
    public static ZlgStatus StartCan(nint channelHandle) =>
        (ZlgStatus)ZCAN_StartCAN(channelHandle);

    /// <summary>复位 CAN 通道。<br/>Resets the CAN channel.</summary>
    public static ZlgStatus ResetCan(nint channelHandle) =>
        (ZlgStatus)ZCAN_ResetCAN(channelHandle);

    /// <summary>清除 CAN 缓冲区。<br/>Clears the CAN buffer.</summary>
    public static ZlgStatus ClearBuffer(nint channelHandle) =>
        (ZlgStatus)ZCAN_ClearBuffer(channelHandle);

    /// <summary>获取接收缓冲区中可读帧的数量。<br/>Gets the number of frames available in the receive buffer.</summary>
    public static uint GetReceiveNum(nint channelHandle, ZlgReceiveBufferType type) =>
        ZCAN_GetReceiveNum(channelHandle, (byte)type);

    /// <summary>发送经典 CAN 帧。<br/>Transmits classic CAN frames.</summary>
    public static unsafe uint Transmit(nint channelHandle, Span<NativeTransmitData> frames)
    {
        if (frames.Length == 0)
            return 0;

        fixed (NativeTransmitData* framePointer = frames)
        {
            return ZCAN_Transmit(channelHandle, framePointer, (uint)frames.Length);
        }
    }

    /// <summary>发送 CAN FD 帧。<br/>Transmits CAN FD frames.</summary>
    public static unsafe uint TransmitFd(nint channelHandle, Span<NativeTransmitFdData> frames)
    {
        if (frames.Length == 0)
            return 0;

        fixed (NativeTransmitFdData* framePointer = frames)
        {
            return ZCAN_TransmitFD(channelHandle, framePointer, (uint)frames.Length);
        }
    }

    /// <summary>发送合并数据（合并接收模式）。<br/>Transmits data in merged receive mode.</summary>
    public static unsafe uint TransmitData(nint deviceHandle, Span<NativeDataObject> frames)
    {
        if (frames.Length == 0)
            return 0;

        fixed (NativeDataObject* framePointer = frames)
        {
            return ZCAN_TransmitData(deviceHandle, framePointer, (uint)frames.Length);
        }
    }

    /// <summary>接收经典 CAN 帧。<br/>Receives classic CAN frames.</summary>
    public static unsafe uint Receive(nint channelHandle, Span<NativeReceiveData> frames, int waitTimeMs)
    {
        if (frames.Length == 0)
            return 0;

        fixed (NativeReceiveData* framePointer = frames)
        {
            return ZCAN_Receive(channelHandle, framePointer, (uint)frames.Length, waitTimeMs);
        }
    }

    /// <summary>接收 CAN FD 帧。<br/>Receives CAN FD frames.</summary>
    public static unsafe uint ReceiveFd(nint channelHandle, Span<NativeReceiveFdData> frames, int waitTimeMs)
    {
        if (frames.Length == 0)
            return 0;

        fixed (NativeReceiveFdData* framePointer = frames)
        {
            return ZCAN_ReceiveFD(channelHandle, framePointer, (uint)frames.Length, waitTimeMs);
        }
    }

    /// <summary>接收合并数据（合并接收模式）。<br/>Receives data in merged receive mode.</summary>
    public static unsafe uint ReceiveData(nint deviceHandle, Span<NativeDataObject> frames, int waitTimeMs)
    {
        if (frames.Length == 0)
            return 0;

        fixed (NativeDataObject* framePointer = frames)
        {
            return ZCAN_ReceiveData(deviceHandle, framePointer, (uint)frames.Length, waitTimeMs);
        }
    }

    /// <summary>若非 OK 状态则抛出 ZlgApiException。<br/>Throws ZlgApiException if status is not OK.</summary>
    public static void ThrowIfNotOk(ZlgStatus status, string nativeFunction)
    {
        if (status != ZlgStatus.Ok)
            throw new ZlgApiException(nativeFunction, status);
    }

    private static nint ResolveImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        return string.Equals(libraryName, DllName, StringComparison.OrdinalIgnoreCase)
            ? ZlgNativeLoader.LoadZlgCan()
            : 0;
    }

    /// <summary>P/Invoke: 打开 ZLG 设备。<br/>P/Invoke: Opens a ZLG device.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_OpenDevice")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial nint ZCAN_OpenDevice(uint deviceType, uint deviceIndex, uint reserved);

    /// <summary>P/Invoke: 关闭 ZLG 设备。<br/>P/Invoke: Closes a ZLG device.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_CloseDevice")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint ZCAN_CloseDevice(nint deviceHandle);

    /// <summary>P/Invoke: 查询设备在线状态。<br/>P/Invoke: Queries device online status.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_IsDeviceOnLine")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint ZCAN_IsDeviceOnLine(nint deviceHandle);

    /// <summary>P/Invoke: 获取设备信息。<br/>P/Invoke: Gets device information.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_GetDeviceInf")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial uint ZCAN_GetDeviceInf(nint deviceHandle, NativeDeviceInfo* info);

    /// <summary>P/Invoke: 设置设备参数。<br/>P/Invoke: Sets a device parameter.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_SetValue", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint ZCAN_SetValue(nint deviceHandle, string path, string value);

    /// <summary>P/Invoke: 获取设备参数值。<br/>P/Invoke: Gets a device parameter value.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_GetValue", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial nint ZCAN_GetValue(nint deviceHandle, string path);

    /// <summary>P/Invoke: 初始化 CAN 通道。<br/>P/Invoke: Initializes a CAN channel.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_InitCAN")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial nint ZCAN_InitCAN(nint deviceHandle, uint canIndex, NativeChannelInitConfig* initConfig);

    /// <summary>P/Invoke: 启动 CAN 通道。<br/>P/Invoke: Starts the CAN channel.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_StartCAN")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint ZCAN_StartCAN(nint channelHandle);

    /// <summary>P/Invoke: 复位 CAN 通道。<br/>P/Invoke: Resets the CAN channel.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_ResetCAN")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint ZCAN_ResetCAN(nint channelHandle);

    /// <summary>P/Invoke: 清除 CAN 缓冲区。<br/>P/Invoke: Clears the CAN buffer.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_ClearBuffer")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint ZCAN_ClearBuffer(nint channelHandle);

    /// <summary>P/Invoke: 获取可接收帧数量。<br/>P/Invoke: Gets the number of receivable frames.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_GetReceiveNum")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint ZCAN_GetReceiveNum(nint channelHandle, byte type);

    /// <summary>P/Invoke: 发送经典 CAN 帧。<br/>P/Invoke: Transmits classic CAN frames.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_Transmit")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial uint ZCAN_Transmit(nint channelHandle, NativeTransmitData* frames, uint len);

    /// <summary>P/Invoke: 发送 CAN FD 帧。<br/>P/Invoke: Transmits CAN FD frames.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_TransmitFD")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial uint ZCAN_TransmitFD(nint channelHandle, NativeTransmitFdData* frames, uint len);

    /// <summary>P/Invoke: 发送合并数据。<br/>P/Invoke: Transmits merged data.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_TransmitData")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial uint ZCAN_TransmitData(nint deviceHandle, NativeDataObject* frames, uint len);

    /// <summary>P/Invoke: 接收经典 CAN 帧。<br/>P/Invoke: Receives classic CAN frames.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_Receive")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial uint ZCAN_Receive(nint channelHandle, NativeReceiveData* frames, uint len, int waitTime);

    /// <summary>P/Invoke: 接收 CAN FD 帧。<br/>P/Invoke: Receives CAN FD frames.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_ReceiveFD")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial uint ZCAN_ReceiveFD(nint channelHandle, NativeReceiveFdData* frames, uint len, int waitTime);

    /// <summary>P/Invoke: 接收合并数据。<br/>P/Invoke: Receives merged data.</summary>
    [LibraryImport(DllName, EntryPoint = "ZCAN_ReceiveData")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial uint ZCAN_ReceiveData(nint deviceHandle, NativeDataObject* frames, uint len, int waitTime);
}

/// <summary>
/// ZLG 原生 API 调用异常。包含失败的原生函数名和状态码。<br/>
/// ZLG native API call exception. Contains the failing native function name and status code.
/// </summary>
public sealed class ZlgApiException : Exception
{
    /// <summary>
    /// 创建 ZlgApiException 实例。<br/>
    /// Creates a ZlgApiException instance.
    /// </summary>
    /// <param name="nativeFunction">失败的原生函数名。<br/>The failing native function name.</param>
    /// <param name="status">ZLG 状态码。<br/>The ZLG status code.</param>
    /// <param name="detail">可选的详细描述。<br/>Optional detail description.</param>
    public ZlgApiException(string nativeFunction, ZlgStatus status, string? detail = null)
        : base(detail is null
            ? $"{nativeFunction} failed with ZLG status {status} ({(uint)status})."
            : $"{nativeFunction} failed with ZLG status {status} ({(uint)status}): {detail}.")
    {
        NativeFunction = nativeFunction;
        Status = status;
        Detail = detail;
    }

    /// <summary>失败的原生函数名。<br/>The failing native function name.</summary>
    public string NativeFunction { get; }

    /// <summary>ZLG 状态码。<br/>The ZLG status code.</summary>
    public ZlgStatus Status { get; }

    /// <summary>原生调用附加详情。<br/>Additional native call detail.</summary>
    public string? Detail { get; }
}
