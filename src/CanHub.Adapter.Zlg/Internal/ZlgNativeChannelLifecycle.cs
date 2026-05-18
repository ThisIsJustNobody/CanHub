namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// 基于 ZLG 原生 API 的通道生命周期实现。<br/>
/// ZLG native API backed channel lifecycle implementation.
/// </summary>
internal sealed class ZlgNativeChannelLifecycle : IZlgChannelLifecycle
{
    /// <summary>共享实例。<br/>Shared instance.</summary>
    public static ZlgNativeChannelLifecycle Instance { get; } = new();

    private ZlgNativeChannelLifecycle()
    {
    }

    /// <inheritdoc />
    public nint OpenChannel(ZlgDeviceLeaseEntry device, ZlgChannelOpenSpec spec)
    {
        var busParameters = spec.BusParameters;
        var key = spec.Key;
        var resolved = spec.ResolvedOptions;
        nint channelHandle = 0;

        try
        {
            ConfigureChannelBeforeInit(device, key.ChannelIndex, busParameters);

            var initConfig = NativeChannelInitConfig.CreateCanFd(
                resolved.WorkMode,
                resolved.AccCode,
                resolved.AccMask);
            channelHandle = ZlgNative.InitCan(
                device.DeviceHandle,
                checked((uint)key.ChannelIndex),
                initConfig);

            if (busParameters.TerminationEnabled.HasValue)
            {
                ZlgNative.ThrowIfNotOk(
                    ZlgNative.SetInternalResistance(
                        device.DeviceHandle,
                        checked((uint)key.ChannelIndex),
                        busParameters.TerminationEnabled.Value),
                    "ZCAN_SetValue(initenal_resistance)");
            }

            ZlgNative.ThrowIfNotOk(ZlgNative.StartCan(channelHandle), "ZCAN_StartCAN");
            ZlgNative.ThrowIfNotOk(ZlgNative.ClearBuffer(channelHandle), "ZCAN_ClearBuffer");
            return channelHandle;
        }
        catch
        {
            if (channelHandle != 0)
            {
                var resetStatus = ZlgNative.ResetCan(channelHandle);
                if (resetStatus != ZlgStatus.Ok)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"CanHub.Zlg failed to reset channel after open rollback: ZCAN_ResetCAN returned {resetStatus}.");
                }
            }

            throw;
        }
    }

    /// <inheritdoc />
    public bool CloseChannel(nint channelHandle, int channelIndex, Action<CanStatusEvent> publishStatus)
    {
        if (channelHandle == 0)
            return true;

        var status = ZlgNative.ResetCan(channelHandle);
        if (status != ZlgStatus.Ok)
        {
            publishStatus(CanStatusEvent.Create(
                CanStatusKind.Driver,
                CanStatusCode.NativeDriverError,
                CanStatusSeverity.Warning,
                channelIndex: channelIndex,
                nativeStatusCode: (uint)status,
                message: $"ZLG close operation failed: ZCAN_ResetCAN returned {status}."));
        }

        return true;
    }

    private static void ConfigureChannelBeforeInit(
        ZlgDeviceLeaseEntry device,
        int channelIndex,
        CanBusParameters busParameters)
    {
        var nativeChannelIndex = checked((uint)channelIndex);
        ZlgNative.ThrowIfNotOk(
            ZlgNative.SetArbitrationBitrate(
                device.DeviceHandle,
                nativeChannelIndex,
                checked((uint)busParameters.ArbitrationBitrate)),
            "ZCAN_SetValue(canfd_abit_baud_rate)");
        if (busParameters.IsFd)
        {
            ZlgNative.ThrowIfNotOk(
                ZlgNative.SetDataBitrate(
                    device.DeviceHandle,
                    nativeChannelIndex,
                    checked((uint)(busParameters.DataBitrate
                        ?? throw new ArgumentNullException(nameof(busParameters.DataBitrate), "CAN FD data bitrate must be specified.")))),
                "ZCAN_SetValue(canfd_dbit_baud_rate)");
        }

        ZlgNative.ThrowIfNotOk(
            ZlgNative.SetCanFdStandard(
                device.DeviceHandle,
                nativeChannelIndex,
                busParameters.IsNonIsoFd),
            "ZCAN_SetValue(canfd_standard)");
    }
}
