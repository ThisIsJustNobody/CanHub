using System.Collections.Concurrent;
using CanHub.Adapter.Vector.Internal;
using CanHub.Core;
using vxlapi_NET;

namespace CanHub.Adapter.Vector;

/// <summary>
/// Vector CAN 适配器提供者。通过 Vector XL Driver API 访问 Vector CAN 硬件。<br/>
/// Vector CAN adapter provider. Accesses Vector CAN hardware via the Vector XL Driver API.
/// </summary>
public sealed class VectorAdapterProvider : ICanAdapterProvider
{
    private static readonly VectorDriver s_driver = new();
    private static readonly ConcurrentDictionary<VectorChannelKey, VectorChannelLeaseEntry> s_channels = new();
    private static readonly SemaphoreSlim s_channelGate = new(1, 1);

    /// <inheritdoc />
    public string AdapterId => "vector";

    /// <inheritdoc />
    public string DisplayName => "Vector CAN";

    /// <inheritdoc />
    public CanAdapterManifest Manifest { get; } = new(
        "vector", "Vector CAN", new[] { "vector" },
        platform: "windows",
        exclusivity: ExclusivityModel.ChannelLevel,
        capabilities: [
            new CanCapability("can-fd", false, "CAN FD support"),
            new CanCapability("classic-can", false, "Classic CAN support"),
        ],
        supportsChannelScan: true);

    /// <summary>
    /// 打开 Vector CAN 总线会话。使用通道级别的租约管理，相同通道的重复打开共享底层驱动端口，
    /// 并通过配置指纹检测冲突。<br/>
    /// Opens a Vector CAN bus session. Uses channel-level lease management; repeated opens
    /// on the same channel share the underlying driver port, with conflict detection via
    /// configuration fingerprinting.
    /// </summary>
    public async ValueTask<ICanBus> OpenAsync(CanOpenContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var deviceType = VectorDeviceTypeMapper.Resolve(context.Endpoint.Device);
            var deviceIndex = GetParameterInt(context.Endpoint, "deviceIndex", 0);
            var channelIndex = context.Endpoint.ChannelIndex ?? 0;
            ValidateNativeOptions(context.Options.NativeOptions);
            var key = new VectorChannelKey(deviceType, deviceIndex, channelIndex);
            var canonicalLocator = $"vector://{deviceType}?deviceIndex={deviceIndex}&channelIndex={channelIndex}";
            var fingerprintOptions = new CanOpenOptions
            {
                BusParameters = context.Options.BusParameters,
                NativeOptions = context.Options.NativeOptions,
            };
            var fingerprint = LeaseConflictDetector.ComputeFingerprint(
                context.Endpoint,
                fingerprintOptions,
                canonicalLocator);

            await s_channelGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (s_channels.TryGetValue(key, out var existing))
                {
                    if (!existing.TryAddReference())
                    {
                        throw new CanException("vector", CanErrorCategory.AdapterError,
                            $"Vector channel '{key}' is still closing after a receive-loop stop timeout. Restart the process or reset the device before reopening it.");
                    }

                    if (!LeaseConflictDetector.FingerprintsMatch(existing.Fingerprint, fingerprint))
                    {
                        existing.ReleaseReference();
                        throw new CanException("vector", CanErrorCategory.ConfigurationConflict,
                            $"Configuration conflict for Vector channel '{key}'. Close existing session first.");
                    }

                    existing.ConfigureRecovery(context.Options.Recovery);
                    return new VectorBus(existing, ReleaseChannel, ReleaseChannelAsync);
                }

                var driver = s_driver;
                await driver.AcquireAsync();

                try
                {
                    var isFd = context.Options.BusParameters.IsFd;
                    var displayName = $"Vector {context.Endpoint.Device} Channel {channelIndex}";
                    var entry = await CreateChannelEntryAsync(
                        key, driver, fingerprint, isFd, displayName, context, ct);

                    s_channels[key] = entry;
                    return new VectorBus(entry, ReleaseChannel, ReleaseChannelAsync);
                }
                catch
                {
                    await driver.ReleaseAsync();
                    throw;
                }
            }
            finally
            {
                s_channelGate.Release();
            }
        }
        catch (CanException ex)
        {
            throw EnrichOpenException(ex, context);
        }
    }

    /// <summary>
    /// 扫描可用的 Vector 硬件通道。调用 XL_GetDriverConfig 枚举所有通道并转换为 CanChannelInfo。<br/>
    /// Scans available Vector hardware channels. Calls XL_GetDriverConfig to enumerate all
    /// channels and converts them to CanChannelInfo.
    /// </summary>
    public async ValueTask<CanChannelScanResult> ScanAsync(
        ScanOptions? options = null, CancellationToken ct = default)
    {
        var driver = s_driver;
        await driver.AcquireAsync();

        try
        {
            var config = new XLClass.xl_driver_config();
            var status = VectorDriver.Driver.XL_GetDriverConfig(ref config);
            if (status != XLDefine.XL_Status.XL_SUCCESS)
            {
                return new CanChannelScanResult(diagnostics: [
                    CreateDriverConfigFailureDiagnostic(status)
                ]);
            }

            var channels = new List<CanChannelInfo>();
            for (int i = 0; i < config.channelCount && i < config.channel.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var ch = config.channel[i];
                channels.Add(VectorChannelScanMapper.FromChannelConfig(ch));
            }
            return new CanChannelScanResult(channels);
        }
        finally
        {
            await driver.ReleaseAsync();
        }
    }

    private static async ValueTask<VectorChannelLeaseEntry> CreateChannelEntryAsync(
        VectorChannelKey key,
        VectorDriver driver,
        byte[] fingerprint,
        bool isFd,
        string displayName,
        CanOpenContext context,
        CancellationToken ct)
    {
        var channelMask = ResolveChannelMask(key.DeviceType, key.DeviceIndex, key.ChannelIndex);

        var port = new VectorChannelPort(driver, channelMask, key.ChannelIndex);
        FrameBroadcastHub? hub = null;
        try
        {
            await port.ActivateAsync(context, ct);

            var seqGen = new CanSequenceGenerator();
            hub = new FrameBroadcastHub(seqGen);
            var openSpec = new VectorChannelOpenSpec(context);
            var entry = new VectorChannelLeaseEntry(
                key,
                driver,
                port,
                hub,
                fingerprint,
                isFd,
                displayName,
                openSpec,
                context.Options.Recovery,
                VectorNativeChannelLifecycle.Instance);
            CheckUnsupportedBusParameters(context.Options.BusParameters, entry.PublishStatus, port.LogicalChannelIndex);
            entry.StartReceiveLoop();

            return entry;
        }
        catch
        {
            hub?.Dispose();
            await port.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask ReleaseChannelAsync(
        VectorChannelLeaseEntry entry, CancellationToken ct = default)
    {
        await s_channelGate.WaitAsync(ct).ConfigureAwait(false);
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
            if (entry.Dispose())
                s_channels.TryRemove(entry.Key, out _);
        }
        finally
        {
            s_channelGate.Release();
        }
    }

    private static void ReleaseChannel(VectorChannelLeaseEntry entry)
    {
        s_channelGate.Wait();
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
            if (entry.Dispose())
                s_channels.TryRemove(entry.Key, out _);
        }
        finally
        {
            s_channelGate.Release();
        }
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
                "vector",
                CanErrorCategory.InvalidEndpoint,
                endpoint,
                nativeFunction: $"endpoint query parameter '{key}'");
        }

        return result;
    }

    private static void ValidateNativeOptions(object? nativeOptions)
    {
        if (nativeOptions is null or VectorOpenOptions)
            return;

        throw new CanException("vector", CanErrorCategory.ConfigurationConflict,
            $"Vector adapter NativeOptions must be {nameof(VectorOpenOptions)} when specified.");
    }

    internal static ScanDiagnostic CreateDriverConfigFailureDiagnostic(XLDefine.XL_Status status)
    {
        var vendorCode = (int)status;
        return new ScanDiagnostic(
            CanErrorCategory.AdapterError,
            $"XL_GetDriverConfig failed: {VectorDriver.Driver.XL_GetErrorString(status)}",
            vendorCode,
            adapterId: "vector",
            hint: "检查 Vector XL Driver 是否已安装、设备是否连接，或当前进程是否能加载 Vector 驱动运行时。",
            details: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nativeFunction"] = "XL_GetDriverConfig",
                ["vendorCode"] = vendorCode.ToString(),
            });
    }

    private static CanException EnrichOpenException(CanException ex, CanOpenContext context)
    {
        var details = BuildOpenDetails(context);
        foreach (var detail in ex.Details)
            details[detail.Key] = detail.Value;
        if (!string.IsNullOrWhiteSpace(ex.NativeFunction))
            details["nativeFunction"] = ex.NativeFunction;
        if (ex.VendorCode.HasValue)
            details["vendorCode"] = ex.VendorCode.Value.ToString();

        return new CanException(
            ex.AdapterId,
            ex.Category,
            ex.Message,
            ex,
            ex.Endpoint ?? context.Endpoint,
            ex.NativeFunction,
            ex.VendorCode,
            ex.Recoverability,
            ex.Hint ?? "检查 Vector 驱动是否已安装、设备是否被占用、设备索引/通道索引是否存在，以及总线参数是否被其他会话占用。",
            details);
    }

    private static Dictionary<string, string> BuildOpenDetails(CanOpenContext context)
    {
        var bp = context.Options.BusParameters;
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["endpoint"] = context.Endpoint.ToString(),
            ["device"] = context.Endpoint.Device,
            ["deviceIndex"] = context.Endpoint.Parameters.TryGetValue("deviceIndex", out var deviceIndex)
                ? deviceIndex
                : "0",
            ["channelIndex"] = (context.Endpoint.ChannelIndex ?? 0).ToString(),
            ["arbitrationBitrate"] = bp.ArbitrationBitrate.ToString(),
            ["isFd"] = bp.IsFd.ToString(),
        };
        if (bp.DataBitrate.HasValue)
            details["dataBitrate"] = bp.DataBitrate.Value.ToString();

        return details;
    }

    /// <summary>
    /// 解析通道掩码。匹配 hwType + hwIndex（设备索引）+ channelIndex（设备本地通道索引）。
    /// 使用 xl_channel_config.channelIndex（全局索引）计算掩码。
    /// 若未找到匹配通道或掩码为 0 则抛出异常。
    /// </summary>
    private static ulong ResolveChannelMask(
        XLDefine.XL_HardwareType deviceType, int deviceIndex, int channelIndex)
    {
        var mask = VectorDriver.Driver.XL_GetChannelMask(deviceType, deviceIndex, channelIndex);
        if (mask != 0)
            return mask;

        throw new CanException("vector", CanErrorCategory.InvalidEndpoint,
            $"No matching Vector channel found for {deviceType}[{deviceIndex}] channel {channelIndex}.");
    }

    /// <summary>
    /// 检查用户显式指定的 CanBusParameters 中适配器不支持的部分，触发 ConfigurationIgnored 状态事件。
    /// </summary>
    private static void CheckUnsupportedBusParameters(
        CanBusParameters bp, Action<CanStatusEvent> publishStatus, int channelIndex)
    {
        ReportIfSet(bp.TerminationEnabled, nameof(CanBusParameters.TerminationEnabled));
        ReportIfSet(bp.SelfAck, nameof(CanBusParameters.SelfAck));

        void ReportIfSet(bool? value, string name)
        {
            if (!value.HasValue) return;

            if (bp.UnsupportedParameterPolicy == CanBusParameterPolicy.Require)
            {
                throw new CanException("vector", CanErrorCategory.ConfigurationConflict,
                    $"参数 '{name}' 不被 Vector 适配器支持，且策略为 Require。");
            }

            publishStatus(CanStatusEvent.ConfigurationIgnored(
                name, "Vector adapter does not support this parameter.", channelIndex));
        }
    }
}
