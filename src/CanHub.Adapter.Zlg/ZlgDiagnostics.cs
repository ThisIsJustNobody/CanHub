using CanHub.Adapter.Zlg.Internal;

namespace CanHub.Adapter.Zlg;

/// <summary>
/// ZLG 运行时诊断入口。用于检查平台、原生库加载和设备扫描是否正常。<br/>
/// ZLG runtime diagnostics entry point. Checks platform, native library loading, and device scanning.
/// </summary>
public static class ZlgDiagnostics
{
    /// <summary>
    /// 检查 ZLG 运行时状态。该方法会加载原生库并执行设备扫描，但不会打开具体通道。<br/>
    /// Checks ZLG runtime status. This method loads the native library and scans devices, but does not open a channel.
    /// </summary>
    public static ValueTask<ZlgRuntimeDiagnosticReport> CheckRuntimeAsync(
        ScanOptions? scanOptions = null,
        CancellationToken ct = default) =>
        CheckRuntimeAsync(ZlgRuntimeDiagnosticProbe.Instance, scanOptions, ct);

    internal static async ValueTask<ZlgRuntimeDiagnosticReport> CheckRuntimeAsync(
        IZlgRuntimeDiagnosticProbe probe,
        ScanOptions? scanOptions = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ct.ThrowIfCancellationRequested();

        var diagnostics = new List<ScanDiagnostic>();
        IReadOnlyList<CanChannelInfo> channels = Array.Empty<CanChannelInfo>();
        string? nativeLibraryPath = null;
        var nativeLibraryFound = false;
        var nativeLibraryLoaded = false;
        var scanSucceeded = false;

        if (!probe.IsWindows)
        {
            diagnostics.Add(new ScanDiagnostic(
                CanErrorCategory.AdapterError,
                "ZLG adapter is supported only on Windows.",
                adapterId: "zlg",
                hint: "请在安装 ZLG 驱动的 Windows 环境中使用 CanHub.Adapter.Zlg。"));

            return CreateReport();
        }

        try
        {
            nativeLibraryPath = probe.FindNativeLibrary();
            nativeLibraryFound = !string.IsNullOrWhiteSpace(nativeLibraryPath);
            if (!nativeLibraryFound)
            {
                diagnostics.Add(new ScanDiagnostic(
                    CanErrorCategory.AdapterError,
                    "ZLG native driver library path is empty.",
                    adapterId: "zlg",
                    hint: "检查 CanHub.Adapter.Zlg 原生资产是否复制到输出目录。"));

                return CreateReport();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsRuntimeDiagnosticException(ex))
        {
            diagnostics.Add(ZlgExceptionMapper.ToScanDiagnostic(ex));
            return CreateReport();
        }

        try
        {
            probe.LoadNativeLibrary();
            nativeLibraryLoaded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsRuntimeDiagnosticException(ex))
        {
            diagnostics.Add(ZlgExceptionMapper.ToScanDiagnostic(ex));
            return CreateReport();
        }

        try
        {
            var scan = await probe.ScanAsync(scanOptions, ct).ConfigureAwait(false);
            scanSucceeded = true;
            channels = scan.Channels;
            diagnostics.AddRange(scan.Diagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CanException ex)
        {
            diagnostics.Add(new ScanDiagnostic(
                ex.Category,
                ex.Message,
                ex.VendorCode,
                ex.Recoverability,
                ex.AdapterId,
                ex.Endpoint?.ToString(),
                ex.Hint,
                ex.Details));
        }
        catch (Exception ex)
        {
            diagnostics.Add(new ScanDiagnostic(
                CanErrorCategory.AdapterError,
                $"ZLG runtime diagnostic scan failed: {ex.GetType().Name}: {ex.Message}",
                adapterId: "zlg"));
        }

        return CreateReport();

        ZlgRuntimeDiagnosticReport CreateReport() => new(
            probe.IsWindows,
            probe.ProcessArchitecture,
            nativeLibraryPath,
            nativeLibraryFound,
            nativeLibraryLoaded,
            scanSucceeded,
            channels,
            diagnostics);
    }

    private static bool IsRuntimeDiagnosticException(Exception exception) =>
        exception is CanException || ZlgExceptionMapper.IsNativeBoundaryException(exception);
}
