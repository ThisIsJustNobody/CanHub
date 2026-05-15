using System.Diagnostics;
using System.Security;

namespace CanHub.Adapter.Zlg.Tests;

[TestClass]
public sealed class ZlgNativeAssetTargetsTests
{
    [TestMethod(DisplayName = "ZLG targets prefer RuntimeIdentifier over PlatformTarget")]
    [DataRow("win-x86", "AnyCPU", "x86")]
    [DataRow("win-x64", "x86", "x64")]
    [DataRow("win10-x86", "AnyCPU", "x86")]
    [DataRow("win10-x64", "x86", "x64")]
    public void NativeTargets_RuntimeIdentifierWinsOverPlatformTarget(
        string runtimeIdentifier,
        string platformTarget,
        string expectedMarker)
    {
        var copiedMarker = CopyNativeMarker(runtimeIdentifier, platformTarget);

        Assert.AreEqual(expectedMarker, copiedMarker);
    }

    [TestMethod(DisplayName = "ZLG targets fall back to PlatformTarget when RuntimeIdentifier is absent")]
    [DataRow("x86", "x86")]
    [DataRow("x64", "x64")]
    public void NativeTargets_PlatformTargetFallbackSelectsNativeAsset(
        string platformTarget,
        string expectedMarker)
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("PlatformTarget fallback is host-build behavior for Windows.");

        var copiedMarker = CopyNativeMarker(runtimeIdentifier: null, platformTarget);

        Assert.AreEqual(expectedMarker, copiedMarker);
    }

    private static string CopyNativeMarker(string? runtimeIdentifier, string platformTarget)
    {
        var workspace = Path.Combine(
            Path.GetTempPath(),
            "CanHub.Zlg.Targets." + Guid.NewGuid().ToString("N"));

        try
        {
            var packageRoot = Path.Combine(workspace, "package");
            var targetDirectory = Path.Combine(packageRoot, "buildTransitive");
            Directory.CreateDirectory(targetDirectory);
            var targetPath = Path.Combine(targetDirectory, "CanHub.Adapter.Zlg.targets");
            File.Copy(FindTargetsFile(), targetPath);

            WriteNativeMarker(packageRoot, "win-x86", "x86");
            WriteNativeMarker(packageRoot, "win-x64", "x64");

            var outputPath = Path.Combine(workspace, "out") + Path.DirectorySeparatorChar;
            var projectPath = Path.Combine(workspace, "copy.proj");
            File.WriteAllText(projectPath, $"""
                <Project>
                  <Import Project="{SecurityElement.Escape(targetPath)}" />
                  <PropertyGroup>
                    <OutputPath>{SecurityElement.Escape(outputPath)}</OutputPath>
                  </PropertyGroup>
                  <Target Name="Build" />
                </Project>
                """);

            var result = RunMsBuild(projectPath, runtimeIdentifier, platformTarget);
            Assert.AreEqual(0, result.ExitCode, result.Output);

            var copiedMarker = Path.Combine(outputPath, "zlgcan.dll");
            Assert.IsTrue(File.Exists(copiedMarker), result.Output);
            return File.ReadAllText(copiedMarker);
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    private static void WriteNativeMarker(string packageRoot, string rid, string marker)
    {
        var nativeDirectory = Path.Combine(packageRoot, "runtimes", rid, "native");
        Directory.CreateDirectory(nativeDirectory);
        File.WriteAllText(Path.Combine(nativeDirectory, "zlgcan.dll"), marker);
    }

    private static (int ExitCode, string Output) RunMsBuild(
        string projectPath,
        string? runtimeIdentifier,
        string platformTarget)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("/t:Build");
        process.StartInfo.ArgumentList.Add("/v:m");
        process.StartInfo.ArgumentList.Add("/nologo");
        process.StartInfo.ArgumentList.Add("/p:PlatformTarget=" + platformTarget);
        if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
            process.StartInfo.ArgumentList.Add("/p:RuntimeIdentifier=" + runtimeIdentifier);
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;

        process.Start();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        if (!process.WaitForExit(milliseconds: 30_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("dotnet msbuild did not exit within 30 seconds.");
        }

        return (process.ExitCode, output);
    }

    private static string FindTargetsFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "CanHub.Adapter.Zlg",
                "build",
                "CanHub.Adapter.Zlg.targets");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate src/CanHub.Adapter.Zlg/build/CanHub.Adapter.Zlg.targets from the test output directory.");
        return string.Empty;
    }
}
