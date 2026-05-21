using System.Reflection;
using System.Runtime.InteropServices;
using vxlapi_NET;

namespace CanHub.Adapter.Vector.Internal;

/// <summary>
/// Vector 原生 DLL 加载器。为 vxlapi_NET 的 P/Invoke 解析命名空间化输出目录。<br/>
/// Vector native DLL loader. Resolves vxlapi_NET P/Invoke calls from the namespaced output directory.
/// </summary>
internal static class VectorNativeLoader
{
    private const string X86DllName = "vxlapi.dll";
    private const string X64DllName = "vxlapi64.dll";

    private static readonly object s_gate = new();
    private static bool s_resolverRegistered;
    private static nint s_loadedHandle;
    private static string? s_loadedPath;

    /// <summary>
    /// 已加载的 Vector 原生 DLL 完整路径。<br/>
    /// The full path of the loaded Vector native DLL.
    /// </summary>
    public static string? LoadedPath
    {
        get
        {
            lock (s_gate)
            {
                return s_loadedPath;
            }
        }
    }

    /// <summary>
    /// 确保已为 vxlapi_NET 程序集注册原生 DLL 解析器。<br/>
    /// Ensures the native DLL resolver is registered for the vxlapi_NET assembly.
    /// </summary>
    public static void EnsureRegistered()
    {
        lock (s_gate)
        {
            if (s_resolverRegistered)
                return;

            NativeLibrary.SetDllImportResolver(typeof(XLDriver).Assembly, ResolveImport);
            s_resolverRegistered = true;
        }
    }

    private static nint ResolveImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var expectedDllName = Environment.Is64BitProcess ? X64DllName : X86DllName;
        if (!string.Equals(libraryName, expectedDllName, StringComparison.OrdinalIgnoreCase))
            return 0;

        lock (s_gate)
        {
            if (s_loadedHandle != 0)
                return s_loadedHandle;

            var dllPath = FindNativeDll(expectedDllName);
            s_loadedHandle = NativeLibrary.Load(dllPath);
            s_loadedPath = dllPath;
            return s_loadedHandle;
        }
    }

    private static string FindNativeDll(string dllName)
    {
        var archFolder = Environment.Is64BitProcess ? "x64" : "x86";
        foreach (var root in CandidateRoots())
        {
            var path = Path.Combine(root, "canhub", "vector", archFolder, dllName);
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        throw new DllNotFoundException(
            $"Could not find {dllName}. Ensure CanHub.Adapter.Vector native assets are copied to canhub/vector/{archFolder} under the output directory.");
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return AppContext.BaseDirectory;

        var assemblyLocation = typeof(VectorNativeLoader).GetTypeInfo().Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
            yield return Path.GetDirectoryName(assemblyLocation)!;

        yield return Environment.CurrentDirectory;
    }
}
