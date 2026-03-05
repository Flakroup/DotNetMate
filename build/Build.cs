using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "AllUnderscoreLocalParameterName")]
class Build : NukeBuild
{
    [Parameter("Configuration to build")] readonly Configuration Configuration = Configuration.Release;

    [Parameter("NuGet feed URL for publishing")]
    readonly string NuGetSource = "https://flakroup.pkgs.visualstudio.com/_packaging/Flakroup/nuget/v3/index.json";

    [Parameter("NuGet API Key (use 'az' for Azure Artifacts)")] readonly string NuGetApiKey = "az";

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";
    AbsolutePath TestResultsDirectory => ArtifactsDirectory / "test-results";

    Project ToolProject => Solution.GetProject("DotNetMateTool");

    Target Clean =>
        _ => _
            .Before(Restore)
            .Description("Clean bin/obj folders and artifacts directory")
            .Executes(() =>
            {
                Log.Information("🧹 Cleaning build artifacts...");

                SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(static d => d.DeleteDirectory());
                TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(static d => d.DeleteDirectory());

                var toolBin = RootDirectory / "DotNetMateTool" / "bin";
                var toolObj = RootDirectory / "DotNetMateTool" / "obj";
                var visualizerBin = RootDirectory / "GitLogVisualizer" / "bin";
                var visualizerObj = RootDirectory / "GitLogVisualizer" / "obj";

                if (toolBin.DirectoryExists())
                    toolBin.DeleteDirectory();

                if (toolObj.DirectoryExists())
                    toolObj.DeleteDirectory();

                if (visualizerBin.DirectoryExists())
                    visualizerBin.DeleteDirectory();

                if (visualizerObj.DirectoryExists())
                    visualizerObj.DeleteDirectory();

                ArtifactsDirectory.CreateOrCleanDirectory();

                Log.Information("✅ Clean completed");
            });

    Target Restore =>
        _ => _
            .Description("Restore NuGet packages")
            .Executes(() =>
            {
                Log.Information("📦 Restoring NuGet packages...");

                DotNetRestore(s => s
                    .SetProjectFile(Solution));

                Log.Information("✅ Restore completed");
            });

    Target Compile =>
        _ => _
            .DependsOn(Info, Clean, Restore)
            .Description("Build the solution")
            .Executes(() =>
            {
                Log.Information($"🔨 Building solution in {Configuration} mode...");

                DotNetBuild(s => s
                    .SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    .EnableNoRestore());

                Log.Information("✅ Build completed successfully");
            });

    Target Test =>
        _ => _
            .DependsOn(Compile)
            .Description("Run unit tests")
            .Triggers(Benchmark)
            .Executes(() =>
            {
                Log.Information("🧪 Running unit tests...");

                TestResultsDirectory.CreateOrCleanDirectory();

                DotNetTest(s => s
                    .SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers("trx"));

                Log.Information("✅ All tests passed!");
            });

    Target Benchmark =>
        _ => _
            .Description("Run performance benchmarks")
            .Executes(() =>
            {
                Log.Information("⚡ Running benchmarks...");

                var benchmarkProject = Solution.GetProject("DotNetMate.Benchmarks");
                var jobArg = IsLocalBuild ? "" : "--job Dry";

                DotNet($"run --project {benchmarkProject} --configuration {Configuration} --no-build -- -f * --join {jobArg}");

                Log.Information("✅ Benchmarks completed");
            });

    Target Pack =>
        _ => _
            .DependsOn(Test)
            .Description("Create NuGet packages")
            .Executes(() =>
            {
                Log.Information("📦 Creating NuGet packages...");

                PackagesDirectory.CreateOrCleanDirectory();

                DotNetPack(s => s
                    .SetProject(ToolProject)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetOutputDirectory(PackagesDirectory));

                var packages = PackagesDirectory.GlobFiles("*.nupkg");

                foreach (var package in packages)
                    Log.Information($"  📦 Created: {package.Name}");

                Log.Information("✅ Pack completed");
            });

    Target Publish =>
        _ => _
            .DependsOn(Pack)
            .Description("Publish to Azure Artifacts feed")
            .Requires(() => NuGetApiKey)
            .Executes(() =>
            {
                Log.Information("🚀 Publishing to Azure Artifacts...");
                Log.Information($"   Feed: {NuGetSource}");

                var packages = PackagesDirectory.GlobFiles("*.nupkg", "*.snupkg");

                if (!packages.Any())
                {
                    Log.Warning("⚠️  No packages found to publish!");

                    return;
                }

                static bool IsAzureArtifactsFeed(string source) =>
                    source.Contains("pkgs.visualstudio.com", StringComparison.OrdinalIgnoreCase)
                    || source.Contains("pkgs.dev.azure.com", StringComparison.OrdinalIgnoreCase)
                    || source.Contains("pkgs.codedev.ms", StringComparison.OrdinalIgnoreCase);

                static string XmlEscape(string value) => SecurityElement.Escape(value) ?? string.Empty;

                // Azure Artifacts:
                // - `--api-key` is required by dotnet, but is NOT the actual authentication mechanism.
                // - Auth is done via NuGet credentials (NuGet.config) or credential provider.
                // Here we support both:
                // - If a PAT is provided (either via --nuget-api-key or AZURE_DEVOPS_PAT), generate a temporary NuGet.config with credentials.
                // - Otherwise, fall back to whatever is configured on the machine (credential provider / existing config), using api-key "az".
                var isAzureArtifacts = IsAzureArtifactsFeed(NuGetSource);
                var patForAzureArtifacts = string.Empty;

                if (isAzureArtifacts)
                {
                    // If user provided something other than "az", treat it as PAT for Azure Artifacts.
                    if (!string.IsNullOrWhiteSpace(NuGetApiKey)
                        && !string.Equals(NuGetApiKey, "az", StringComparison.OrdinalIgnoreCase))
                        patForAzureArtifacts = NuGetApiKey;
                    else
                        patForAzureArtifacts = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT") ?? string.Empty;
                }

                AbsolutePath tempNuGetConfig = null;
                const string azureSourceName = "AzureArtifacts";

                if (isAzureArtifacts && !string.IsNullOrWhiteSpace(patForAzureArtifacts))
                {
                    tempNuGetConfig = TemporaryDirectory / "nuget.publish.temp.config";

                    // Keep credentials out of global/user config; use a temp config scoped to this build.
                    var configXml =
                        $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""{azureSourceName}"" value=""{XmlEscape(NuGetSource)}"" />
  </packageSources>
  <packageSourceCredentials>
    <{azureSourceName}>
      <add key=""Username"" value=""PAT"" />
      <add key=""ClearTextPassword"" value=""{XmlEscape(patForAzureArtifacts)}"" />
    </{azureSourceName}>
  </packageSourceCredentials>
</configuration>
";

                    File.WriteAllText(tempNuGetConfig, configXml);
                    Log.Information("🔐 Using temporary NuGet.config credentials for Azure Artifacts feed");
                }

                foreach (var package in packages)
                {
                    Log.Information($"   📤 Pushing: {package.Name}");

                    if (tempNuGetConfig != null)
                        // Use the source name so credentials in NuGet.config are picked up.
                        // `--api-key` can be any value for Azure Artifacts; keep "az" as a conventional placeholder.
                        DotNet(
                            $"nuget push \"{package}\" --source \"{azureSourceName}\" --api-key \"az\" --skip-duplicate --configfile \"{tempNuGetConfig}\"",
                            logOutput: false);
                    else
                        DotNetNuGetPush(s => s
                            .SetTargetPath(package)
                            .SetSource(NuGetSource)
                            .SetApiKey(NuGetApiKey)
                            .SetSkipDuplicate(true));
                }

                Log.Information("✅ Publish completed successfully!");
            });

    Target InstallLocal =>
        _ => _
            .DependsOn(Publish)
            .Description("Install the tool locally for testing")
            .Executes(() =>
            {
                Log.Information("🔧 Installing DotNetMateTool locally...");

                // Uninstall first if exists
                try
                {
                    DotNet("tool uninstall -g DotNetMateTool", logOutput: false);
                }
                catch
                {
                    // Tool might not be installed, ignore
                }

                // Install from local package
                DotNet($"tool install -g DotNetMateTool");

                Log.Information("✅ Tool installed! Run: mate --help");
            });

    Target Full =>
        _ => _
            .Description("Full build pipeline: Clean → Restore → Compile → Test → Pack")
            .DependsOn(Clean, Pack)
            .Executes(static () =>
            {
                Log.Information("🎉 Full build pipeline completed successfully!");
            });

    Target CI =>
        _ => _
            .Description("CI pipeline: Restore → Compile → Test")
            .DependsOn(Test)
            .Executes(static () =>
            {
                Log.Information("✅ CI pipeline completed");
            });

    Target Info =>
        _ => _
            .Description("Display build information")
            .Before(Clean)
            .Executes(() =>
            {
                Log.Information("ℹ️  DotNetMate Build Information");
                Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Log.Information($"Solution: {Solution.Name}");
                Log.Information($"Configuration: {Configuration}");
                Log.Information($"Root Directory: {RootDirectory}");
                Log.Information($"Git Branch: {GitRepository?.Branch}");
                Log.Information($"Git Commit: {GitRepository?.Commit}");
                Log.Information($"Build Environment: {(IsLocalBuild ? "Local" : "Server")}");
                Log.Information("");
                Log.Information("📦 Projects:");
                Log.Information("  - DotNetMateTool (main tool)");
                Log.Information("  - DotNetMate.Core (core library)");
                Log.Information("  - GitLogVisualizer (git visualization)");
                Log.Information("");
                Log.Information("🧪 Test Projects:");
                Log.Information("  - DotNetMate.Core.Tests");
                Log.Information("  - GitLogVisualizer.Tests");
                Log.Information("");
                Log.Information("⚡ Benchmark Project:");
                Log.Information("  - DotNetMate.Benchmarks");
            });

    /// <summary>
    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    /// </summary>
    public static int Main()
    {
        Environment.SetEnvironmentVariable("NUKE_TELEMETRY_OPTOUT", "1");
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1");
        Environment.SetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0");
        Environment.SetEnvironmentVariable("DOTNET_NOLOGO", "1");

        return Execute<Build>(static x => x.Test);
    }
}