using System.Collections.Concurrent;
using CanHub.Adapter.Zlg.Internal;
using CanHub.Core;

namespace CanHub.Adapter.Zlg;

/// <summary>
/// ZLG CAN 适配器提供者。通过 ZLG USBCAN 驱动打开设备并创建总线会话。v1 仅主动支持已验证的 USBCANFD_200U。<br/>
/// ZLG CAN adapter provider. Opens devices via the ZLG USBCAN driver and creates bus sessions. v1 actively supports only the validated USBCANFD_200U.
/// </summary>
public sealed class ZlgAdapterProvider : ICanAdapterProvider
{
    private const int DefaultScanDepth = 2;
    private static readonly ConcurrentDictionary<ZlgDeviceKey, ZlgDeviceLeaseEntry> s_devices = new();
    private static readonly ConcurrentDictionary<ZlgChannelKey, ZlgChannelLeaseEntry> s_channels = new();
    private static readonly SemaphoreSlim s_gate = new(1, 1);

    /// <inheritdoc />
    public string AdapterId => "zlg";

    /// <inheritdoc />
    public string DisplayName => "ZLG CAN";

    /// <inheritdoc />
    public CanAdapterManifest Manifest { get; } = new(
        "zlg",
        "ZLG CAN",
        ["zlg"],
        platform: "windows",
        exclusivity: ExclusivityModel.DeviceLevel,
        capabilities:
        [
            new CanCapability("classic-can", false, "Classic CAN support"),
            new CanCapability("can-fd", false, "CAN FD support"),
            new CanCapability("iso-can-fd", false, "ISO CAN FD support"),
            new CanCapability("merged-receive", false, "Device-level merged receive"),
            new CanCapability("internal-termination", false, "ZLG internal termination control"),
        ],
        supportsChannelScan: true);

    /// <summary>
    /// 打开 ZLG CAN 总线。使用共享设备/通道租约管理：同一端点返回同一会话引用，配置冲突时抛出异常。<br/>
    /// Opens a ZLG CAN bus. Uses shared device/channel lease management: the same endpoint returns the same session reference; throws on configuration conflict.
    /// </summary>
    public async ValueTask<ICanBus> OpenAsync(CanOpenContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var capabilities = ZlgDeviceTypeMap.Resolve(context.Endpoint.Device);
            var deviceIndex = GetParameterInt(context.Endpoint, "deviceIndex", 0);
            var channelIndex = context.Endpoint.ChannelIndex ?? 0;
            var resolved = ZlgResolvedOpenOptions.Create(
                capabilities,
                context.Options.BusParameters,
                context.Options.NativeOptions);
            var key = new ZlgChannelKey(capabilities.DeviceTypeId, deviceIndex, channelIndex);
            var canonicalLocator = $"zlg://{capabilities.EndpointName}?deviceIndex={deviceIndex}&channelIndex={channelIndex}";
            var fingerprintOptions = new CanOpenOptions
            {
                BusParameters = context.Options.BusParameters,
                NativeOptions = resolved,
            };
            var fingerprint = LeaseConflictDetector.ComputeFingerprint(
                context.Endpoint,
                fingerprintOptions,
                canonicalLocator);

            await s_gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (s_channels.TryGetValue(key, out var existing))
                {
                    if (!existing.TryAddReference())
                    {
                        throw new CanException("zlg", CanErrorCategory.AdapterError,
                            $"ZLG channel '{key}' is still closing after a receive-loop stop timeout. Restart the process or reset the device before reopening it.");
                    }

                    if (!LeaseConflictDetector.FingerprintsMatch(existing.Fingerprint, fingerprint))
                    {
                        existing.ReleaseReference();
                        throw new CanException("zlg", CanErrorCategory.ConfigurationConflict,
                            $"Configuration conflict for ZLG channel '{key}'. Close existing session first.");
                    }

                    existing.ConfigureRecovery(context.Options.Recovery);
                    return new ZlgBus(existing, ReleaseChannel, ReleaseChannelAsync);
                }

                ZlgDeviceLeaseEntry? device = null;
                var createdDevice = false;
                try
                {
                    device = GetOrOpenDevice(key.DeviceKey, capabilities, resolved.UseMergedReceive, out createdDevice);
                    var entry = CreateChannelEntry(key, device, fingerprint, resolved, context);
                    s_channels[key] = entry;
                    return new ZlgBus(entry, ReleaseChannel, ReleaseChannelAsync);
                }
                catch (Exception ex)
                {
                    if (createdDevice && device is not null)
                    {
                        s_devices.TryRemove(key.DeviceKey, out _);
                        await device.DisposeAsync().ConfigureAwait(false);
                    }

                    if (ex is CanException)
                        throw;

                    if (ZlgExceptionMapper.IsNativeBoundaryException(ex))
                        throw ZlgExceptionMapper.ToCanException(ex, context);

                    throw;
                }
            }
            finally
            {
                s_gate.Release();
            }
        }
        catch (CanException ex)
        {
            throw ZlgExceptionMapper.EnrichOpenException(ex, context);
        }
    }

    /// <summary>
    /// 扫描 ZLG USB 设备。遍历已知设备类型，打开设备获取信息，生成通道列表和诊断信息。仅支持 Windows。<br/>
    /// Scans for ZLG USB devices. Enumerates known device types, opens devices to retrieve information, and produces channel listings with diagnostics. Windows only.
    /// </summary>
    public ValueTask<CanChannelScanResult> ScanAsync(
        ScanOptions? options = null,
        CancellationToken ct = default)
    {
        var channels = new List<CanChannelInfo>();
        var diagnostics = new List<ScanDiagnostic>();

        if (!OperatingSystem.IsWindows())
        {
            diagnostics.Add(new ScanDiagnostic(
                CanErrorCategory.AdapterError,
                "ZLG adapter is supported only on Windows in v1.",
                adapterId: "zlg"));
            return ValueTask.FromResult(new CanChannelScanResult(channels, diagnostics));
        }

        var startIndex = options?.StartIndex ?? 0;
        var minDepth = Math.Max(options?.MinDepth ?? 0, DefaultScanDepth);

        try
        {
            foreach (var capabilities in ZlgDeviceTypeMap.GetScannableDeviceTypes())
            {
                for (var deviceIndex = startIndex; deviceIndex < startIndex + minDepth; deviceIndex++)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!ZlgNative.TryOpenDevice((ZlgDeviceType)capabilities.DeviceTypeId, (uint)deviceIndex, out var handle))
                        continue;

                    try
                    {
                        var info = ZlgNative.GetDeviceInfo(
                            handle,
                            (uint)deviceIndex,
                            (ZlgDeviceType)capabilities.DeviceTypeId);
                        var channelCount = Math.Max(info.CanChannelCount, (byte)capabilities.DefaultChannelCount);
                        for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
                        {
                            channels.Add(CreateScanChannelInfo(
                                capabilities,
                                info,
                                deviceIndex,
                                channelIndex));
                        }
                    }
                    catch (ZlgApiException ex)
                    {
                        diagnostics.Add(ZlgExceptionMapper.ToScanDiagnostic(ex));
                    }
                    finally
                    {
                        var closeStatus = ZlgNative.CloseDevice(handle);
                        if (closeStatus != ZlgStatus.Ok)
                        {
                            diagnostics.Add(new ScanDiagnostic(
                                CanErrorCategory.AdapterError,
                                $"ZCAN_CloseDevice returned {closeStatus} while closing scanned ZLG device.",
                                (int)closeStatus,
                                adapterId: "zlg"));
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ZlgExceptionMapper.IsNativeBoundaryException(ex))
        {
            diagnostics.Add(ZlgExceptionMapper.ToScanDiagnostic(ex));
        }

        return ValueTask.FromResult(new CanChannelScanResult(channels, diagnostics));
    }

    private static ZlgDeviceLeaseEntry GetOrOpenDevice(
        ZlgDeviceKey key,
        ZlgDeviceCapabilities capabilities,
        bool useMergedReceive,
        out bool created)
    {
        created = false;
        if (s_devices.TryGetValue(key, out var existing))
        {
            if (existing.IsClosingOrDisposed)
            {
                throw new CanException("zlg", CanErrorCategory.AdapterError,
                    $"ZLG device '{key}' is still closing after a receive-loop stop timeout. Restart the process or reset the device before reopening it.");
            }

            if (existing.MergedReceive != useMergedReceive)
            {
                throw new CanException("zlg", CanErrorCategory.ConfigurationConflict,
                    $"ZLG device '{key}' already uses UseMergedReceive={existing.MergedReceive}. Mixed receive strategies are not supported.");
            }

            return existing;
        }

        var device = ZlgDeviceLeaseEntry.Open(key, capabilities, useMergedReceive);
        s_devices[key] = device;
        created = true;
        return device;
    }

    private static ZlgChannelLeaseEntry CreateChannelEntry(
        ZlgChannelKey key,
        ZlgDeviceLeaseEntry device,
        byte[] fingerprint,
        ZlgResolvedOpenOptions resolved,
        CanOpenContext context)
    {
        var busParameters = context.Options.BusParameters;
        FrameBroadcastHub? hub = null;
        nint channelHandle = 0;
        var channelReferenceAdded = false;

        try
        {
            var openSpec = new ZlgChannelOpenSpec(key, busParameters, resolved);
            channelHandle = ZlgNativeChannelLifecycle.Instance.OpenChannel(device, openSpec);

            hub = new FrameBroadcastHub(new CanSequenceGenerator());
            var displayName = $"ZLG {device.Capabilities.EndpointName} Device {key.DeviceIndex} Channel {key.ChannelIndex}";
            var entry = new ZlgChannelLeaseEntry(
                key,
                device,
                channelHandle,
                hub,
                fingerprint,
                busParameters.IsFd,
                resolved.DefaultTransmitType,
                displayName,
                openSpec,
                context.Options.Recovery,
                ZlgNativeChannelLifecycle.Instance);

            device.AddChannelReference();
            channelReferenceAdded = true;
            CheckUnsupportedTimingParameters(busParameters, entry.PublishStatus, key.ChannelIndex);
            entry.StartReceiveLoop();
            return entry;
        }
        catch
        {
            if (channelReferenceAdded)
                device.ReleaseChannelReference();
            if (channelHandle != 0)
                ZlgNativeChannelLifecycle.Instance.CloseChannel(channelHandle, key.ChannelIndex, _ => { });
            hub?.Dispose();
            throw;
        }
    }

    private static async ValueTask ReleaseChannelAsync(
        ZlgChannelLeaseEntry entry,
        CancellationToken ct = default)
    {
        await s_gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!s_channels.TryGetValue(entry.Key, out var current) ||
                !ReferenceEquals(current, entry))
            {
                return;
            }

            var remaining = entry.ReleaseReference();
            if (remaining > 0)
                return;

            entry.MarkClosing();
            if (!entry.Dispose())
                return;
            s_channels.TryRemove(entry.Key, out _);

            var deviceRemaining = entry.Device.ReleaseChannelReference();
            if (deviceRemaining <= 0)
            {
                entry.Device.MarkClosing();
                if (entry.Device.Dispose())
                    s_devices.TryRemove(entry.Key.DeviceKey, out _);
            }
        }
        finally
        {
            s_gate.Release();
        }
    }

    private static void ReleaseChannel(ZlgChannelLeaseEntry entry)
    {
        s_gate.Wait();
        try
        {
            if (!s_channels.TryGetValue(entry.Key, out var current) ||
                !ReferenceEquals(current, entry))
            {
                return;
            }

            var remaining = entry.ReleaseReference();
            if (remaining > 0)
                return;

            entry.MarkClosing();
            if (!entry.Dispose())
                return;
            s_channels.TryRemove(entry.Key, out _);

            var deviceRemaining = entry.Device.ReleaseChannelReference();
            if (deviceRemaining <= 0)
            {
                entry.Device.MarkClosing();
                if (entry.Device.Dispose())
                    s_devices.TryRemove(entry.Key.DeviceKey, out _);
            }
        }
        finally
        {
            s_gate.Release();
        }
    }

    private static CanChannelInfo CreateScanChannelInfo(
        ZlgDeviceCapabilities capabilities,
        ZlgDeviceInfo info,
        int deviceIndex,
        int channelIndex)
    {
        var deviceName = string.IsNullOrWhiteSpace(info.HardwareType)
            ? capabilities.EndpointName
            : $"{capabilities.EndpointName} {info.HardwareType}".Trim();
        if (!string.IsNullOrWhiteSpace(info.SerialNumber))
            deviceName = $"{deviceName} SN:{info.SerialNumber}";

        return new CanChannelInfo(
            "zlg",
            deviceName,
            deviceIndex,
            channelIndex,
            nativeChannelIndex: channelIndex,
            endpoint: $"zlg://{capabilities.EndpointName}?deviceIndex={deviceIndex}&channelIndex={channelIndex}",
            availability: CanChannelAvailability.Available,
            capabilities:
            [
                new CanCapability("classic-can", false),
                new CanCapability("can-fd", false),
                new CanCapability("iso-can-fd", false),
                new CanCapability("merged-receive", false),
                new CanCapability("internal-termination", false),
            ],
            vendorName: "ZLG",
            hardwareId: $"{capabilities.EndpointName}:{deviceIndex}",
            serialNumber: string.IsNullOrWhiteSpace(info.SerialNumber) ? null : info.SerialNumber,
            displayName: $"ZLG {capabilities.EndpointName} #{deviceIndex} CH{channelIndex}",
            recommendedBusParameters: CanBusParameters.Classic500k);
    }

    private static int GetParameterInt(CanEndpoint endpoint, string key, int defaultValue)
    {
        if (endpoint.Parameters.TryGetValue(key, out var value))
            return ParseNonNegativeInt(endpoint, key, value);

        return defaultValue;
    }

    private static int ParseNonNegativeInt(CanEndpoint endpoint, string key, string value)
    {
        if (!int.TryParse(value, out var result) || result < 0)
        {
            throw new CanException(
                "zlg",
                CanErrorCategory.InvalidEndpoint,
                endpoint,
                nativeFunction: $"endpoint query parameter '{key}'");
        }

        return result;
    }

    private static void CheckUnsupportedTimingParameters(
        CanBusParameters busParameters,
        Action<CanStatusEvent> publishStatus,
        int channelIndex)
    {
        ReportIfSet(busParameters.ArbitrationTseg1, nameof(CanBusParameters.ArbitrationTseg1));
        ReportIfSet(busParameters.ArbitrationTseg2, nameof(CanBusParameters.ArbitrationTseg2));
        ReportIfSet(busParameters.ArbitrationSjw, nameof(CanBusParameters.ArbitrationSjw));
        ReportIfSet(busParameters.DataTseg1, nameof(CanBusParameters.DataTseg1));
        ReportIfSet(busParameters.DataTseg2, nameof(CanBusParameters.DataTseg2));
        ReportIfSet(busParameters.DataSjw, nameof(CanBusParameters.DataSjw));

        void ReportIfSet(int? value, string name)
        {
            if (!value.HasValue)
                return;

            if (busParameters.UnsupportedParameterPolicy == CanBusParameterPolicy.Require)
            {
                throw new CanException("zlg", CanErrorCategory.ConfigurationConflict,
                    $"参数 '{name}' 不被 ZLG 适配器支持，且策略为 Require。");
            }

            publishStatus(CanStatusEvent.ConfigurationIgnored(
                name,
                "ZLG adapter v1 uses bitrate keys verified by ZlgDriverProbe, not explicit timing registers.",
                channelIndex));
        }
    }
}
