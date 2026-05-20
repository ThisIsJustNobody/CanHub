namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 异常映射工具。将原生异常分类并转换为 CanException。<br/>
/// ZLG exception mapping utility. Classifies native exceptions and converts them to CanException.
/// </summary>
internal static class ZlgExceptionMapper
{
    /// <summary>
    /// 判断异常是否属于原生边界异常（应映射为 CanException）。<br/>
    /// Determines whether an exception is a native boundary exception that should be mapped to CanException.
    /// </summary>
    public static bool IsNativeBoundaryException(Exception exception) =>
        exception is ZlgApiException
            or PlatformNotSupportedException
            or DllNotFoundException
            or BadImageFormatException
            or EntryPointNotFoundException
            or System.ComponentModel.Win32Exception;

    /// <summary>
    /// 将原生异常映射为 CanException。根据异常类型提供对应的中文错误信息。<br/>
    /// Maps a native exception to a CanException with appropriate error information based on exception type.
    /// </summary>
    public static CanException ToCanException(Exception exception, CanOpenContext? context = null)
    {
        if (exception is CanException canException)
            return canException;

        return exception switch
        {
            ZlgApiException apiException => CreateApiException(apiException, context),
            PlatformNotSupportedException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG adapter native support is unavailable on this platform: {exception.Message}",
                exception,
                endpoint: context?.Endpoint,
                hint: "ZLG 适配器当前仅支持 Windows 原生驱动环境。",
                details: BuildOpenDetails(context)),
            DllNotFoundException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG native driver library could not be loaded: {exception.Message}",
                exception,
                endpoint: context?.Endpoint,
                hint: "检查 CanHub.Adapter.Zlg 原生资产是否复制到输出目录，或驱动 DLL 是否缺失。",
                details: BuildOpenDetails(context)),
            BadImageFormatException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG native driver library architecture is incompatible with the current process: {exception.Message}",
                exception,
                endpoint: context?.Endpoint,
                hint: "检查进程位数与 zlgcan.dll 架构是否一致。",
                details: BuildOpenDetails(context)),
            EntryPointNotFoundException => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG native driver library is missing a required entry point: {exception.Message}",
                exception,
                endpoint: context?.Endpoint,
                hint: "检查 zlgcan.dll 版本是否与 CanHub.Adapter.Zlg 期望的 API 兼容。",
                details: BuildOpenDetails(context)),
            _ => new CanException(
                "zlg",
                CanErrorCategory.AdapterError,
                $"ZLG adapter native operation failed: {exception.Message}",
                exception,
                endpoint: context?.Endpoint,
                details: BuildOpenDetails(context)),
        };
    }

    /// <summary>
    /// 将原生异常映射为扫描诊断，并保留可读提示和结构化详情。<br/>
    /// Maps a native exception to scan diagnostics while preserving hints and structured details.
    /// </summary>
    public static ScanDiagnostic ToScanDiagnostic(Exception exception, CanOpenContext? context = null)
    {
        var mapped = ToCanException(exception, context);
        return new ScanDiagnostic(
            mapped.Category,
            mapped.Message,
            mapped.VendorCode,
            mapped.Recoverability,
            mapped.AdapterId,
            mapped.Endpoint?.ToString(),
            mapped.Hint,
            mapped.Details);
    }

    /// <summary>
    /// 为打开流程中的 CanException 补充端点、打开参数和排障提示。<br/>
    /// Enriches a CanException from the open path with endpoint, open parameters, and troubleshooting hints.
    /// </summary>
    public static CanException EnrichOpenException(CanException exception, CanOpenContext context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(context);

        var details = BuildOpenDetails(context);
        foreach (var detail in exception.Details)
            details[detail.Key] = detail.Value;
        if (!string.IsNullOrWhiteSpace(exception.NativeFunction))
            details["nativeFunction"] = exception.NativeFunction;
        if (exception.VendorCode.HasValue)
            details["vendorCode"] = exception.VendorCode.Value.ToString();

        return new CanException(
            exception.AdapterId,
            exception.Category,
            exception.Message,
            exception,
            exception.Endpoint ?? context.Endpoint,
            exception.NativeFunction,
            exception.VendorCode,
            exception.Recoverability,
            exception.Hint ?? "检查 ZLG 驱动是否已安装、设备是否被占用、刚插拔后驱动是否就绪、deviceIndex/channelIndex 是否存在，以及总线参数是否与已有会话冲突。",
            details);
    }

    private static CanException CreateApiException(ZlgApiException exception, CanOpenContext? context)
    {
        var details = BuildOpenDetails(context);
        details["nativeFunction"] = exception.NativeFunction;
        details["vendorCode"] = ((uint)exception.Status).ToString();
        if (!string.IsNullOrWhiteSpace(exception.Detail))
            details["nativeDetail"] = exception.Detail;
        if (ZlgNativeLoader.LoadedPath is { Length: > 0 } loadedPath)
            details["nativeLibraryPath"] = loadedPath;

        return new CanException(
            "zlg",
            CanErrorCategory.AdapterError,
            $"ZLG native call '{exception.NativeFunction}' failed with status {exception.Status} ({(uint)exception.Status}).",
            exception,
            endpoint: context?.Endpoint,
            nativeFunction: exception.NativeFunction,
            vendorCode: (int)exception.Status,
            hint: "设备可能被占用、刚插拔、驱动未就绪，或 deviceIndex/channelIndex 不存在；请确认驱动状态、设备索引、通道索引和总线参数。",
            details: details);
    }

    private static Dictionary<string, string> BuildOpenDetails(CanOpenContext? context)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context is null)
            return details;

        var bp = context.Options.BusParameters;
        details["endpoint"] = context.Endpoint.ToString();
        details["device"] = context.Endpoint.Device;
        details["deviceIndex"] = context.Endpoint.Parameters.TryGetValue("deviceIndex", out var deviceIndex)
            ? deviceIndex
            : "0";
        details["channelIndex"] = (context.Endpoint.ChannelIndex ?? 0).ToString();
        details["arbitrationBitrate"] = bp.ArbitrationBitrate.ToString();
        details["isFd"] = bp.IsFd.ToString();
        if (bp.DataBitrate.HasValue)
            details["dataBitrate"] = bp.DataBitrate.Value.ToString();

        return details;
    }
}
