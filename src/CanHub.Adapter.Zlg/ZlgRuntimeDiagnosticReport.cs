namespace CanHub.Adapter.Zlg;

/// <summary>
/// ZLG 运行时诊断报告。汇总平台、原生库加载和设备扫描状态。<br/>
/// ZLG runtime diagnostic report. Summarizes platform, native library loading, and device scan status.
/// </summary>
public sealed class ZlgRuntimeDiagnosticReport
{
    internal ZlgRuntimeDiagnosticReport(
        bool isWindows,
        string processArchitecture,
        string? nativeLibraryPath,
        bool nativeLibraryFound,
        bool nativeLibraryLoaded,
        bool scanSucceeded,
        IReadOnlyList<CanChannelInfo>? channels,
        IReadOnlyList<ScanDiagnostic>? diagnostics)
    {
        IsWindows = isWindows;
        ProcessArchitecture = string.IsNullOrWhiteSpace(processArchitecture) ? "unknown" : processArchitecture;
        NativeLibraryPath = string.IsNullOrWhiteSpace(nativeLibraryPath) ? null : nativeLibraryPath;
        NativeLibraryFound = nativeLibraryFound;
        NativeLibraryLoaded = nativeLibraryLoaded;
        ScanSucceeded = scanSucceeded;
        Channels = channels is null || channels.Count == 0
            ? Array.AsReadOnly(Array.Empty<CanChannelInfo>())
            : Array.AsReadOnly(channels.ToArray());
        Diagnostics = diagnostics is null || diagnostics.Count == 0
            ? Array.AsReadOnly(Array.Empty<ScanDiagnostic>())
            : Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>当前平台是否为 Windows。<br/>Whether the current platform is Windows.</summary>
    public bool IsWindows { get; }

    /// <summary>当前进程架构。<br/>Current process architecture.</summary>
    public string ProcessArchitecture { get; }

    /// <summary>找到或加载的 zlgcan.dll 路径。<br/>Path to the found or loaded zlgcan.dll.</summary>
    public string? NativeLibraryPath { get; }

    /// <summary>是否找到了 ZLG 原生库。<br/>Whether the ZLG native library was found.</summary>
    public bool NativeLibraryFound { get; }

    /// <summary>是否成功加载了 ZLG 原生库。<br/>Whether the ZLG native library was loaded successfully.</summary>
    public bool NativeLibraryLoaded { get; }

    /// <summary>是否完成设备扫描。<br/>Whether device scanning completed.</summary>
    public bool ScanSucceeded { get; }

    /// <summary>扫描到的通道。<br/>Scanned channels.</summary>
    public IReadOnlyList<CanChannelInfo> Channels { get; }

    /// <summary>诊断信息。<br/>Diagnostics.</summary>
    public IReadOnlyList<ScanDiagnostic> Diagnostics { get; }

    /// <summary>扫描结果中是否存在可打开通道。<br/>Whether the scan result contains an openable channel.</summary>
    public bool HasOpenableChannel => Channels.Any(channel => channel.CanOpen);

    /// <summary>运行时是否已就绪，可尝试打开至少一个通道。<br/>Whether the runtime is ready to try opening at least one channel.</summary>
    public bool IsReady => IsWindows && NativeLibraryFound && NativeLibraryLoaded && ScanSucceeded && HasOpenableChannel;
}
