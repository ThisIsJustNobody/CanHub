using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CanHub.Adapter.Zlg.Internal;

/// <summary>
/// ZLG 原生 DLL 加载器。在多个候选位置搜索 zlgcan.dll 并配置本地 DLL 搜索路径。<br/>
/// ZLG native DLL loader. Searches for zlgcan.dll across multiple candidate locations and configures native DLL search paths.
/// </summary>
public static class ZlgNativeLoader
{
    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;
    private const uint LoadLibrarySearchUserDirs = 0x00000400;

    private static readonly object s_gate = new();
    private static readonly List<nint> s_directoryCookies = [];
    private static readonly HashSet<string> s_registeredDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static nint s_loadedHandle;
    private static string? s_loadedPath;
    private static bool s_defaultSearchConfigured;

    /// <summary>
    /// 已加载的 zlgcan.dll 的完整路径。<br/>
    /// The full path of the loaded zlgcan.dll.
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
    /// 加载 zlgcan.dll。搜索候选路径，配置 DLL 目录，加载原生库。<br/>
    /// Loads zlgcan.dll. Searches candidate paths, configures DLL directories, and loads the native library.
    /// </summary>
    public static nint LoadZlgCan()
    {
        lock (s_gate)
        {
            if (s_loadedHandle != 0)
                return s_loadedHandle;

            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("ZLG zlgcan.dll probing is supported only on Windows.");

            var dllPath = FindZlgCanDll();
            ConfigureDllDirectories(Path.GetDirectoryName(dllPath)!);
            s_loadedHandle = NativeLibrary.Load(dllPath);
            s_loadedPath = dllPath;
            return s_loadedHandle;
        }
    }

    /// <summary>
    /// 查找 zlgcan.dll。按架构选择合适的子目录，遍历多个候选根路径。<br/>
    /// Finds zlgcan.dll. Selects the appropriate architecture subdirectory and searches across multiple candidate roots.
    /// </summary>
    public static string FindZlgCanDll()
    {
        var archFolder = Environment.Is64BitProcess ? "zlgcan_x64" : "zlgcan_x86";
        var runtimeFolder = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        foreach (var root in CandidateRoots())
        {
            foreach (var path in new[]
            {
                Path.Combine(root, "libs", "Zlg", archFolder, "zlgcan.dll"),
                Path.Combine(root, "runtimes", runtimeFolder, "native", "zlgcan.dll"),
                Path.Combine(root, "CanHubResources", "Zlg", Environment.Is64BitProcess ? "x64" : "x86", "zlgcan.dll"),
                Path.Combine(root, "zlgcan.dll"),
            })
            {
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }
        }

        throw new DllNotFoundException(
            $"Could not find zlgcan.dll for {archFolder}. Ensure CanHub.Adapter.Zlg native assets are copied to the output directory.");
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return AppContext.BaseDirectory;

        var assemblyLocation = typeof(ZlgNativeLoader).GetTypeInfo().Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
            yield return Path.GetDirectoryName(assemblyLocation)!;

        yield return Environment.CurrentDirectory;
    }

    private static void ConfigureDllDirectories(string nativeRoot)
    {
        if (!s_defaultSearchConfigured)
        {
            if (!SetDefaultDllDirectories(LoadLibrarySearchDefaultDirs | LoadLibrarySearchUserDirs))
                throw CreateWin32Exception(nameof(SetDefaultDllDirectories));

            s_defaultSearchConfigured = true;
        }

        foreach (var path in EnumerateNativeDependencyDirectories(nativeRoot))
        {
            var fullPath = Path.GetFullPath(path);
            if (!s_registeredDirectories.Add(fullPath))
                continue;

            var cookie = AddDllDirectory(fullPath);
            if (cookie == 0)
                throw CreateWin32Exception(nameof(AddDllDirectory));

            s_directoryCookies.Add(cookie);
        }
    }

    private static IEnumerable<string> EnumerateNativeDependencyDirectories(string nativeRoot)
    {
        yield return nativeRoot;

        var kernelRoot = Path.Combine(nativeRoot, "kerneldlls");
        if (!Directory.Exists(kernelRoot))
            yield break;

        yield return kernelRoot;
        foreach (var directory in Directory.EnumerateDirectories(kernelRoot, "*", SearchOption.AllDirectories))
            yield return directory;
    }

    private static Win32Exception CreateWin32Exception(string functionName)
    {
        var error = Marshal.GetLastPInvokeError();
        return new Win32Exception(error, $"{functionName} failed while configuring ZLG native dependency directories.");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint AddDllDirectory(string newDirectory);
}
