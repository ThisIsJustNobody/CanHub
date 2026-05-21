namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 运行时诊断探针。生产实现访问真实运行时，测试实现避免触碰硬件。<br/>
/// ZLG runtime diagnostic probe. The production implementation touches the real runtime; tests can avoid hardware.
/// </summary>
internal interface IZlgRuntimeDiagnosticProbe
{
    bool IsWindows { get; }
    string ProcessArchitecture { get; }
    string FindNativeLibrary();
    void LoadNativeLibrary();
    ValueTask<CanChannelScanResult> ScanAsync(ScanOptions? options, CancellationToken ct = default);
}
