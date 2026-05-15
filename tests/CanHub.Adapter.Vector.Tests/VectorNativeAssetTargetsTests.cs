using System.Diagnostics;
using System.Security;

namespace CanHub.Adapter.Vector.Tests;

[TestClass]
public sealed class VectorNativeAssetTargetsTests
{
    [TestMethod(DisplayName = "Vector targets select Windows native asset by RuntimeIdentifier")]
    [DataRow("win-x86", "x64", "vxlapi.dll")]
    [DataRow("win-x64", "x86", "vxlapi64.dll")]
    [DataRow("win10-x86", "x64", "vxlapi.dll")]
    [DataRow("win10-x64", "x86", "vxlapi64.dll")]
    public void NativeTargets_WindowsRuntimeIdentifierSelectsNativeAsset(
        string runtimeIdentifier,
        string platformTarget,
        string expectedLink)
    {
        var contentLinks = EvaluateContentLinks(runtimeIdentifier, platformTarget);

        CollectionAssert.Contains(contentLinks, expectedLink);
    }

    [TestMethod(DisplayName = "Vector targets do not select Windows assets for non-Windows RID")]
    public void NativeTargets_NonWindowsRuntimeIdentifierDoesNotSelectNativeAsset()
    {
        var contentLinks = EvaluateContentLinks("linux-x64", "AnyCPU");

        Assert.AreEqual(0, contentLinks.Length);
    }

    private static string[] EvaluateContentLinks(string runtimeIdentifier, string platformTarget)
    {
        var workspace = Path.Combine(
            Path.GetTempPath(),
            "CanHub.Vector.Targets." + Guid.NewGuid().ToString("N"));

        try
        {
            var packageRoot = Path.Combine(workspace, "package");
            var targetDirectory = Path.Combine(packageRoot, "buildTransitive");
            Directory.CreateDirectory(targetDirectory);
            var targetPath = Path.Combine(targetDirectory, "CanHub.Adapter.Vector.targets");
            File.Copy(FindTargetsFile(), targetPath);

            WriteNativeMarker(packageRoot, "win-x86", "vxlapi.dll");
            WriteNativeMarker(packageRoot, "win-x64", "vxlapi64.dll");

            var outputPath = Path.Combine(workspace, "out") + Path.DirectorySeparatorChar;
            var contentPath = Path.Combine(outputPath, "content.txt");
            var projectPath = Path.Combine(workspace, "evaluate.csproj");
            File.WriteAllText(projectPath, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputPath>{SecurityElement.Escape(outputPath)}</OutputPath>
                  </PropertyGroup>
                  <Import Project="{SecurityElement.Escape(targetPath)}" />
                  <Target Name="WriteContentLinks">
                    <MakeDir Directories="{SecurityElement.Escape(outputPath)}" />
                    <WriteLinesToFile
                      File="{SecurityElement.Escape(contentPath)}"
                      Lines="@(Content->'%(Link)')"
                      Overwrite="true" />
                  </Target>
                </Project>
                """);

            var result = RunMsBuild(projectPath, runtimeIdentifier, platformTarget);
            Assert.AreEqual(0, result.ExitCode, result.Output);

            if (!File.Exists(contentPath))
                return [];

            return File.ReadAllLines(contentPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    private static void WriteNativeMarker(string packageRoot, string rid, string fileName)
    {
        var nativeDirectory = Path.Combine(packageRoot, "runtimes", rid, "native");
        Directory.CreateDirectory(nativeDirectory);
        File.WriteAllText(Path.Combine(nativeDirectory, fileName), rid);
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
        process.StartInfo.ArgumentList.Add("/t:WriteContentLinks");
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
}
