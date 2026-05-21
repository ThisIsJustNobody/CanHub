using System.Diagnostics;
using System.Security;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorNativeAssetTargetsTests
{
    [TestMethod(DisplayName = "Vector targets copy Windows native asset to namespaced folder by RuntimeIdentifier")]
    [DataRow("win-x86", "x64", "x86", "vxlapi.dll")]
    [DataRow("win-x64", "x86", "x64", "vxlapi64.dll")]
    [DataRow("win10-x86", "x64", "x86", "vxlapi.dll")]
    [DataRow("win10-x64", "x86", "x64", "vxlapi64.dll")]
    public void NativeTargets_WindowsRuntimeIdentifierCopiesNativeAssetToNamespacedFolder(
        string runtimeIdentifier,
        string platformTarget,
        string expectedArch,
        string expectedFileName)
    {
        var result = CopyNativeMarkers(runtimeIdentifier, platformTarget);
        var expectedPath = string.Join('/', "canhub", "vector", expectedArch, expectedFileName);

        CollectionAssert.Contains(result.RelativeFiles, expectedPath);
        CollectionAssert.DoesNotContain(result.RelativeFiles, "vxlapi.dll");
        CollectionAssert.DoesNotContain(result.RelativeFiles, "vxlapi64.dll");
        Assert.AreEqual(expectedArch, result.FileContents[expectedPath]);
    }

    [TestMethod(DisplayName = "Vector targets do not copy Windows assets for non-Windows RID")]
    public void NativeTargets_NonWindowsRuntimeIdentifierDoesNotCopyNativeAsset()
    {
        var result = CopyNativeMarkers("linux-x64", "AnyCPU");

        Assert.AreEqual(0, result.RelativeFiles.Length);
    }

    private static CopyResult CopyNativeMarkers(string runtimeIdentifier, string platformTarget)
    {
        var workspace = Path.Combine(
            Path.GetTempPath(),
            "CanHub.Vector.Targets." + Guid.NewGuid().ToString("N"));

        var packageRoot = Path.Combine(workspace, "package");
        var outputPath = Path.Combine(workspace, "out") + Path.DirectorySeparatorChar;

        try
        {
            var targetDirectory = Path.Combine(packageRoot, "buildTransitive");
            Directory.CreateDirectory(targetDirectory);
            var targetPath = Path.Combine(targetDirectory, "CanHub.Adapter.Vector.targets");
            File.Copy(FindTargetsFile(), targetPath);

            WriteNativeMarker(packageRoot, "win-x86", "vxlapi.dll", "x86");
            WriteNativeMarker(packageRoot, "win-x64", "vxlapi64.dll", "x64");

            var projectPath = Path.Combine(workspace, "evaluate.proj");
            File.WriteAllText(projectPath, $"""
                <Project>
                  <Import Project="{SecurityElement.Escape(targetPath)}" />
                  <PropertyGroup>
                    <OutputPath>{SecurityElement.Escape(outputPath)}</OutputPath>
                  </PropertyGroup>
                  <Target Name="Build" />
                </Project>
                """);

            var msbuildResult = RunMsBuild(projectPath, runtimeIdentifier, platformTarget);
            Assert.AreEqual(0, msbuildResult.ExitCode, msbuildResult.Output);

            if (!Directory.Exists(outputPath))
                return new CopyResult([], new Dictionary<string, string>(StringComparer.Ordinal));

            var relativeFiles = Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(outputPath, path))
                .Select(path => path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .ToArray();
            var fileContents = relativeFiles.ToDictionary(
                path => path,
                path => File.ReadAllText(Path.Combine(outputPath, path)),
                StringComparer.Ordinal);

            return new CopyResult(relativeFiles, fileContents);
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    private static void WriteNativeMarker(string packageRoot, string rid, string fileName, string marker)
    {
        var nativeDirectory = Path.Combine(packageRoot, "buildTransitive", "native", rid);
        Directory.CreateDirectory(nativeDirectory);
        File.WriteAllText(Path.Combine(nativeDirectory, fileName), marker);
    }

    private static (int ExitCode, string Output) RunMsBuild(
        string projectPath,
        string runtimeIdentifier,
        string platformTarget)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("/t:Build");
        process.StartInfo.ArgumentList.Add("/v:m");
        process.StartInfo.ArgumentList.Add("/nologo");
        process.StartInfo.ArgumentList.Add("/p:RuntimeIdentifier=" + runtimeIdentifier);
        process.StartInfo.ArgumentList.Add("/p:PlatformTarget=" + platformTarget);
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
                "CanHub.Adapter.Vector",
                "build",
                "CanHub.Adapter.Vector.targets");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate src/CanHub.Adapter.Vector/build/CanHub.Adapter.Vector.targets from the test output directory.");
        return string.Empty;
    }

    private sealed record CopyResult(string[] RelativeFiles, IReadOnlyDictionary<string, string> FileContents);
}
