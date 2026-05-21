using System.Runtime.InteropServices;

namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// 访问真实 ZLG 运行时的诊断探针。<br/>
/// Diagnostic probe that accesses the real ZLG runtime.
/// </summary>
internal sealed class ZlgRuntimeDiagnosticProbe : IZlgRuntimeDiagnosticProbe
{
    public static ZlgRuntimeDiagnosticProbe Instance { get; } = new();

    private ZlgRuntimeDiagnosticProbe() { }

    public bool IsWindows => OperatingSystem.IsWindows();

    public string ProcessArchitecture => RuntimeInformation.ProcessArchitecture.ToString();

    public string FindNativeLibrary() => ZlgNativeLoader.FindZlgCanDll();

    public void LoadNativeLibrary() => _ = ZlgNativeLoader.LoadZlgCan();

    public ValueTask<CanChannelScanResult> ScanAsync(ScanOptions? options, CancellationToken ct = default) =>
        new ZlgAdapterProvider().ScanAsync(options, ct);
}
