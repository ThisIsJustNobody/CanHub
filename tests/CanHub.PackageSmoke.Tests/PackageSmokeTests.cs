using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace CanHub.PackageSmoke.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PackageSmokeTests
{
    private const string NuGetOrg = "https://api.nuget.org/v3/index.json";
    private static readonly SemaphoreSlim s_packageFeedGate = new(1, 1);
    private static PackageFeed? s_packageFeed;

    [TestMethod(DisplayName = "Virtual adapter package can be consumed by a user project")]
    public async Task VirtualAdapterPackage_CanBeConsumedByUserProject()
    {
        var result = await RunFixtureAsync("VirtualConsumer", "virtual-ok", TestContext.CancellationToken);

        StringAssert.Contains(result.StandardOutput, "virtual-ok");
    }

    [TestMethod(DisplayName = "Vector adapter package adds wrapper assembly and native library")]
    public async Task VectorAdapterPackage_AddsWrapperAssemblyAndNativeLibrary()
    {
        var result = await RunFixtureAsync("VectorConsumer", "vector-ok", TestContext.CancellationToken);

        StringAssert.Contains(result.StandardOutput, "vector-ok");
    }

    [TestMethod(DisplayName = "Vector adapter package publishes namespaced native library")]
    public async Task VectorAdapterPackage_PublishesNamespacedNativeLibrary()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Vector publish smoke test executes a Windows runtime publish.");

        var result = await PublishAndRunFixtureAsync("VectorConsumer", "vector-ok", TestContext.CancellationToken);

        StringAssert.Contains(result.StandardOutput, "vector-ok");
    }

    [TestMethod(DisplayName = "Vector adapter nupkg contains wrapper and native assets")]
    public async Task VectorAdapterPackage_ContainsExpectedPackageEntries()
    {
        var feed = await EnsurePackageFeedAsync(TestContext.CancellationToken);

        AssertPackageContains(
            feed,
            "CanHub.Adapter.Vector",
            "THIRD-PARTY-NOTICES.md",
            "lib/net10.0/vxlapi_NET.dll",
            "buildTransitive/native/win-x64/vxlapi64.dll",
            "buildTransitive/native/win-x86/vxlapi.dll");
    }

    [TestMethod(DisplayName = "ZLG adapter package copies native library tree")]
    public async Task ZlgAdapterPackage_CopiesNativeLibraryTree()
    {
        var result = await RunFixtureAsync("ZlgConsumer", "zlg-ok", TestContext.CancellationToken);

        StringAssert.Contains(result.StandardOutput, "zlg-ok");
    }

    [TestMethod(DisplayName = "ZLG adapter RID build keeps native library tree namespaced")]
    public async Task ZlgAdapterPackage_RidBuildKeepsNativeLibraryTreeNamespaced()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("ZLG RID smoke test executes a Windows runtime build.");

        var result = await RunFixtureAsync("ZlgConsumer", "zlg-ok", TestContext.CancellationToken, runtimeIdentifier: "win-x64");

        StringAssert.Contains(result.StandardOutput, "zlg-ok");
    }

    [TestMethod(DisplayName = "ZLG adapter nupkg contains native library tree and license")]
    public async Task ZlgAdapterPackage_ContainsExpectedPackageEntries()
    {
        var feed = await EnsurePackageFeedAsync(TestContext.CancellationToken);

        AssertPackageContains(
            feed,
            "CanHub.Adapter.Zlg",
            "buildTransitive/native/win-x64/zlgcan.dll",
            "buildTransitive/native/win-x86/zlgcan.dll",
            "licenses/Zlg/zlgcan License.txt",
            "buildTransitive/native/win-x64/kerneldlls/ZPS/ZPSCANFD_IMPL.dll",
            "buildTransitive/native/win-x86/kerneldlls/ZPS/ZPSCANFD_IMPL.dll");
    }

    [TestMethod(DisplayName = "Vector ASC trace package can be consumed by a user project")]
    public async Task VectorAscPackage_CanBeConsumedByUserProject()
    {
        var result = await RunFixtureAsync("VectorAscConsumer", "vector-asc-ok", TestContext.CancellationToken);

        StringAssert.Contains(result.StandardOutput, "vector-asc-ok");
    }

    [TestMethod(DisplayName = "Vector ASC trace nupkg contains library and readmes")]
    public async Task VectorAscPackage_ContainsExpectedPackageEntries()
    {
        var feed = await EnsurePackageFeedAsync(TestContext.CancellationToken);

        AssertPackageContains(
            feed,
            "CanHub.Trace.VectorAsc",
            "README.md",
            "README.zh-CN.md",
            "lib/net10.0/CanHub.Trace.VectorAsc.dll",
            "lib/net10.0/CanHub.Trace.VectorAsc.xml");
    }

    private async Task<CommandResult> RunFixtureAsync(
        string fixtureName,
        string successMarker,
        CancellationToken cancellationToken,
        string? runtimeIdentifier = null)
    {
        var feed = await EnsurePackageFeedAsync(cancellationToken);
        var projectPath = Path.Combine(feed.RepositoryRoot, "tests", "CanHub.PackageSmoke.Tests", "Fixtures", fixtureName, $"{fixtureName}.csproj");
        CleanFixtureBuildOutput(Path.GetDirectoryName(projectPath)!);

        await RunDotnetAsync(
            feed.RepositoryRoot,
            [
                "restore",
                projectPath,
                "--configfile",
                feed.NuGetConfigPath,
                "--nologo",
                .. RuntimeArguments(runtimeIdentifier),
                .. feed.MsBuildProperties,
            ],
            feed.GlobalPackagesPath,
            cancellationToken);

        var result = await RunDotnetAsync(
            feed.RepositoryRoot,
            [
                "run",
                "--project",
                projectPath,
                "--configuration",
                "Release",
                "--no-restore",
                "--nologo",
                .. RuntimeArguments(runtimeIdentifier),
                .. feed.MsBuildProperties,
            ],
            feed.GlobalPackagesPath,
            cancellationToken);

        StringAssert.Contains(result.StandardOutput, successMarker);
        return result;
    }

    private async Task<CommandResult> PublishAndRunFixtureAsync(
        string fixtureName,
        string successMarker,
        CancellationToken cancellationToken,
        string? runtimeIdentifier = null)
    {
        var feed = await EnsurePackageFeedAsync(cancellationToken);
        var projectPath = Path.Combine(feed.RepositoryRoot, "tests", "CanHub.PackageSmoke.Tests", "Fixtures", fixtureName, $"{fixtureName}.csproj");
        CleanFixtureBuildOutput(Path.GetDirectoryName(projectPath)!);

        var publishPath = Path.Combine(Path.GetTempPath(), "CanHub.PackageSmoke.Publish", Guid.NewGuid().ToString("N"));

        await RunDotnetAsync(
            feed.RepositoryRoot,
            [
                "restore",
                projectPath,
                "--configfile",
                feed.NuGetConfigPath,
                "--nologo",
                .. RuntimeArguments(runtimeIdentifier),
                .. feed.MsBuildProperties,
            ],
            feed.GlobalPackagesPath,
            cancellationToken);

        await RunDotnetAsync(
            feed.RepositoryRoot,
            [
                "publish",
                projectPath,
                "--configuration",
                "Release",
                "--no-restore",
                "--output",
                publishPath,
                "--nologo",
                .. RuntimeArguments(runtimeIdentifier),
                .. feed.MsBuildProperties,
            ],
            feed.GlobalPackagesPath,
            cancellationToken);

        var result = await RunExecutableAsync(
            Path.Combine(publishPath, $"{fixtureName}.exe"),
            publishPath,
            cancellationToken);

        StringAssert.Contains(result.StandardOutput, successMarker);
        return result;
    }

    private static void CleanFixtureBuildOutput(string projectDirectory)
    {
        foreach (var directoryName in new[] { "bin", "obj" })
        {
            var path = Path.Combine(projectDirectory, directoryName);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }

    private static IEnumerable<string> RuntimeArguments(string? runtimeIdentifier)
    {
        if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            yield return "--runtime";
            yield return runtimeIdentifier;
        }
    }

    private static async Task<PackageFeed> EnsurePackageFeedAsync(CancellationToken cancellationToken)
    {
        if (s_packageFeed is not null)
            return s_packageFeed;

        await s_packageFeedGate.WaitAsync(cancellationToken);
        try
        {
            if (s_packageFeed is not null)
                return s_packageFeed;

            var repositoryRoot = FindRepositoryRoot();
            var workspaceRoot = Path.Combine(Path.GetTempPath(), "CanHub.PackageSmoke", Guid.NewGuid().ToString("N"));
            var packageSource = Path.Combine(workspaceRoot, "packages");
            var globalPackages = Path.Combine(workspaceRoot, "nuget-packages");
            var nuGetConfigPath = Path.Combine(workspaceRoot, "NuGet.config");

            Directory.CreateDirectory(packageSource);
            Directory.CreateDirectory(globalPackages);

            WriteNuGetConfig(nuGetConfigPath, packageSource);

            var projectDefs = new (string PackageId, string[] PathParts)[]
            {
                ("CanHub.Abstractions", ["src", "CanHub.Abstractions", "CanHub.Abstractions.csproj"]),
                ("CanHub.Core", ["src", "CanHub.Core", "CanHub.Core.csproj"]),
                ("CanHub.Adapter.Virtual", ["src", "CanHub.Adapter.Virtual", "CanHub.Adapter.Virtual.csproj"]),
                ("CanHub.Adapter.Vector", ["src", "CanHub.Adapter.Vector", "CanHub.Adapter.Vector.csproj"]),
                ("CanHub.Adapter.Zlg", ["src", "CanHub.Adapter.Zlg", "CanHub.Adapter.Zlg.csproj"]),
                ("CanHub.Trace.VectorAsc", ["src", "CanHub.Trace.VectorAsc", "CanHub.Trace.VectorAsc.csproj"]),
            };

            foreach (var (packageId, pathParts) in projectDefs)
            {
                var projectPath = Path.Combine([repositoryRoot, .. pathParts]);

                await RunDotnetAsync(
                    repositoryRoot,
                    ["build", projectPath, "--configuration", "Release", "--nologo"],
                    globalPackages,
                    cancellationToken);

                await RunDotnetAsync(
                    repositoryRoot,
                    ["pack", projectPath, "--configuration", "Release", "--no-build", "--output", packageSource, "--nologo"],
                    globalPackages,
                    cancellationToken);

                var nupkg = Directory.GetFiles(packageSource, $"{packageId}.*.nupkg").FirstOrDefault()
                    ?? throw new InvalidOperationException($"Package not found: {packageId} in {packageSource}");

                Assert.IsTrue(File.Exists(nupkg), $"Expected package was not created: {nupkg}");
            }

            var projects = projectDefs.Select(d =>
            {
                var nupkg = Directory.GetFiles(packageSource, $"{d.PackageId}.*.nupkg").First();
                var version = Path.GetFileNameWithoutExtension(nupkg)[(d.PackageId.Length + 1)..]; // strip "PackageId."
                return new ProjectPackage(d.PackageId,
                    Path.Combine([repositoryRoot, .. d.PathParts]), version);
            }).ToArray();

            var feed = new PackageFeed(
                repositoryRoot,
                packageSource,
                globalPackages,
                nuGetConfigPath,
                [
                    $"-p:CanHubAdapterVirtualPackageVersion={projects.Single(p => p.PackageId == "CanHub.Adapter.Virtual").Version}",
                    $"-p:CanHubAdapterVectorPackageVersion={projects.Single(p => p.PackageId == "CanHub.Adapter.Vector").Version}",
                    $"-p:CanHubAdapterZlgPackageVersion={projects.Single(p => p.PackageId == "CanHub.Adapter.Zlg").Version}",
                    $"-p:CanHubTraceVectorAscPackageVersion={projects.Single(p => p.PackageId == "CanHub.Trace.VectorAsc").Version}",
                ]);

            s_packageFeed = feed;
            return feed;
        }
        finally
        {
            s_packageFeedGate.Release();
        }
    }

    private static void AssertPackageContains(
        PackageFeed feed,
        string packageId,
        params string[] expectedEntries)
    {
        var packagePath = GetPackagePath(feed.PackageSourcePath, packageId);
        using var archive = ZipFile.OpenRead(packagePath);
        var actualEntries = archive.Entries
            .Select(entry => entry.FullName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedEntry in expectedEntries)
        {
            Assert.IsTrue(
                actualEntries.Contains(expectedEntry),
                $"Expected {Path.GetFileName(packagePath)} to contain '{expectedEntry}'.");
        }
    }

    private static string GetPackagePath(string packageSourcePath, string packageId)
        => Directory.GetFiles(packageSourcePath, $"{packageId}.*.nupkg").Single();

    private static string FormatArguments(IEnumerable<string> arguments)
        => string.Join(" ", arguments.Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument));

    private static async Task<CommandResult> RunDotnetAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string globalPackagesPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["NUGET_PACKAGES"] = globalPackagesPath;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet process.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardErrorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        string standardOutput;
        string standardError;
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            standardOutput = await standardOutputTask;
            standardError = await standardErrorTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            Assert.Fail($"dotnet {FormatArguments(arguments)} timed out.");
            throw;
        }

        var result = new CommandResult(
            process.ExitCode,
            standardOutput,
            standardError);

        if (result.ExitCode != 0)
        {
            Assert.Fail(
                $"dotnet {FormatArguments(arguments)} failed with exit code {result.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{result.StandardError}");
        }

        return result;
    }

    private static async Task<CommandResult> RunExecutableAsync(
        string executablePath,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start executable: {executablePath}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardErrorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        string standardOutput;
        string standardError;
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            standardOutput = await standardOutputTask;
            standardError = await standardErrorTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            Assert.Fail($"{executablePath} timed out.");
            throw;
        }

        var result = new CommandResult(
            process.ExitCode,
            standardOutput,
            standardError);

        if (result.ExitCode != 0)
        {
            Assert.Fail(
                $"{executablePath} failed with exit code {result.ExitCode}.{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{result.StandardError}");
        }

        return result;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanHub.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate CanHub.slnx from test output directory.");
    }

    private static void WriteNuGetConfig(string path, string packageSource)
    {
        var document = new XDocument(
            new XElement(
                "configuration",
                new XElement(
                    "packageSources",
                    new XElement("clear"),
                    new XElement("add", new XAttribute("key", "CanHubLocal"), new XAttribute("value", packageSource)),
                    new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", NuGetOrg)))));

        document.Save(path);
    }

    public TestContext TestContext { get; set; }

    private sealed record PackageFeed(
        string RepositoryRoot,
        string PackageSourcePath,
        string GlobalPackagesPath,
        string NuGetConfigPath,
        IReadOnlyList<string> MsBuildProperties);

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record ProjectPackage(string PackageId, string ProjectPath, string Version);
}
