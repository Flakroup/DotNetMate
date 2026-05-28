using FEx.Building;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "AllUnderscoreLocalParameterName")]
class Build : FExBuild, ITagTarget, ITestTarget
{
    string IPackTarget.PackProject => (RootDirectory / "src" / "DotNetMateTool" / "DotNetMateTool.csproj").ToString();

    // IPackTarget uses --no-build, so version properties must be set at Compile time
    public override DotNetBuildSettings GetBuildSettings(
        DotNetBuildSettings settings,
        AbsolutePath solution,
        bool noRestore = true,
        DotNetVerbosity? verbosity = null)
    {
        var baseSettings = base.GetBuildSettings(settings, solution, noRestore, verbosity);

        try
        {
            var gitVersion = (IGitVersionComponent)this;

            if (gitVersion.VersionInfo is null)
                return baseSettings;

            return baseSettings
                .SetInformationalVersion(gitVersion.InformationalVersion)
                .SetAssemblyVersion(gitVersion.VersionInfo.AssemblySemVer)
                .SetFileVersion(gitVersion.VersionInfo.AssemblySemFileVer);
        }
        catch (Exception ex)
        {
            Log.Warning("GitVersion unavailable during Compile - using csproj Version fallback: {Error}", ex.Message);
            return baseSettings;
        }
    }

    Target Info => _ => _
        .DependentFor(Clean, Restore, Compile)
        .Executes(LogBuildInfo);

    Target Clean => _ => _
        .Before(Restore)
        .OnlyWhenStatic(() => !IsServerBuild)
        .Executes(() =>
        {
            (RootDirectory / "src").GlobDirectories("**/bin", "**/obj").ForEach(static d => d.DeleteDirectory());
            (RootDirectory / "tests").GlobDirectories("**/bin", "**/obj").ForEach(static d => d.DeleteDirectory());
            (RootDirectory / "artifacts").CreateOrCleanDirectory();
        });

    public Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => GetRestoreSettings(s, Solution));
        });

    public Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => GetBuildSettings(s, Solution));
        });

    Target Benchmark => _ => _
        .TriggeredBy(((ITestTarget)this).Test)
        .DependsOn(Compile)
        .OnlyWhenDynamic(() => IsLocalBuild, "Full benchmark only for local builds — use BenchmarkGate on CI")
        .Executes(() =>
        {
            var benchmarkProject = Solution.GetProject("DotNetMate.Benchmarks");
            DotNet($"run --project {benchmarkProject} --configuration {Configuration} --no-build -- -f * --join");
        });

    Target BenchmarkGate => _ => _
        .TriggeredBy(((ITestTarget)this).Test)
        .DependsOn(Compile)
        .OnlyWhenDynamic(() => IsServerBuild &&
                              Environment.GetEnvironmentVariable("CI_MERGE_REQUEST_TARGET_BRANCH_NAME") == "dev",
            "BenchmarkGate only runs on CI for MRs targeting dev")
        .Executes(() =>
        {
            var benchmarkProject = Solution.GetProject("DotNetMate.Benchmarks");

            DotNet($"run --project {benchmarkProject} --configuration {Configuration} --no-build -- -f * --join --job short --exporters json");

            var resultsDir = RootDirectory / "BenchmarkDotNet.Artifacts" / "results";
            // BenchmarkDotNet's --exporters json emits *-report-full-compressed.json by default.
            // Glob both shapes so the gate keeps working if a future BDN release switches names.
            var jsonFile = resultsDir.GlobFiles("*-report-full*.json")
                .OrderByDescending(static f => f.ToString())
                .FirstOrDefault();

            if (jsonFile is null)
            {
                Log.Warning("No benchmark JSON results found - skipping gate");
                return;
            }

            using var baselineDoc = JsonDocument.Parse(File.ReadAllText(RootDirectory / "benchmark-baseline.json"));
            var thresholds = baselineDoc.RootElement.GetProperty("thresholds");

            using var resultsDoc = JsonDocument.Parse(File.ReadAllText(jsonFile));
            var failures = new List<string>();

            foreach (var benchmark in resultsDoc.RootElement.GetProperty("Benchmarks").EnumerateArray())
            {
                var method = benchmark.GetProperty("Method").GetString();
                var meanNs = benchmark.GetProperty("Statistics").GetProperty("Mean").GetDouble();
                var meanMs = meanNs / 1_000_000.0;

                if (!thresholds.TryGetProperty(method, out var thresholdProp))
                    continue;

                var failMs = thresholdProp.GetProperty("failMs").GetDouble();

                if (meanMs > failMs)
                {
                    Log.Error("REGRESSION: {Method} = {Mean:F1}ms > threshold {Threshold:F1}ms", method, meanMs, failMs);
                    failures.Add($"{method} ({meanMs:F1}ms > {failMs:F1}ms)");
                }
                else
                {
                    Log.Information("OK: {Method} = {Mean:F1}ms (threshold: {Threshold:F1}ms)", method, meanMs, failMs);
                }
            }

            if (failures.Count > 0)
                throw new Exception($"Benchmark regression detected: {string.Join("; ", failures)}");
        });

    Target InstallLocal => _ => _
        .DependsOn(((IPackTarget)this).Pack)
        .Executes(() =>
        {
            try
            {
                DotNet("tool uninstall -g DotNetMateTool", logOutput: false);
            }
            catch
            {
                // Tool might not be installed
            }

            DotNet($"tool install -g DotNetMateTool --add-source {((IPackTarget)this).PackagesDirectory}");
        });

    Target Full => _ => _
        .DependsOn(Clean, ((IPackTarget)this).Pack)
        .Executes(static () =>
        {
            Log.Information("Full build pipeline completed successfully");
        });

    Target StampChangelog => _ => _
        .Description("Stamps [Unreleased] in CHANGELOG.md and <Version> in csproj with the current GitVersion before packing")
        .DependentFor(((IPackTarget)this).Pack)
        .OnlyWhenDynamic(() => IsServerBuild, "Skipping changelog stamp: not running on CI")
        .Executes(() =>
        {
            var version = ((IGitVersionComponent)this).SemVer;
            var date = DateTime.Now.ToString("yyyy-MM-dd");

            var changelogPath = RootDirectory / "CHANGELOG.md";
            if (File.Exists(changelogPath))
            {
                var content = File.ReadAllText(changelogPath);
                var updated = content.Replace("## [Unreleased]", $"## [{version}] - {date}");

                if (updated != content)
                {
                    File.WriteAllText(changelogPath, updated);
                    Log.Information("Stamped CHANGELOG.md: [Unreleased] -> [{Version}] - {Date}", version, date);
                }
                else
                {
                    Log.Information("No [Unreleased] section in CHANGELOG.md - skipping");
                }
            }

            var csprojPath = RootDirectory / "src" / "DotNetMateTool" / "DotNetMateTool.csproj";
            if (File.Exists(csprojPath))
            {
                var content = File.ReadAllText(csprojPath);
                var updated = Regex.Replace(content, @"<Version>[^<]*</Version>", $"<Version>{version}</Version>");

                if (updated != content)
                {
                    File.WriteAllText(csprojPath, updated);
                    Log.Information("Stamped csproj Version: {Version}", version);
                }
            }
        });

    Target CommitChangelog => _ => _
        .Description("Commits and pushes the stamped CHANGELOG.md after tagging")
        .TriggeredBy(((ITagTarget)this).Tag)
        .OnlyWhenDynamic(() => IsServerBuild, "Skipping changelog commit: not running on CI")
        .Executes(() =>
        {
            var changelogPath = RootDirectory / "CHANGELOG.md";

            if (!File.Exists(changelogPath))
                return;

            // Read version from csproj (stamped by StampChangelog) instead of re-querying GitVersion.
            // After Tag created v{X.Y.Z}, GitVersion would return the NEXT version (X.Y.Z+1),
            // making the commit message inconsistent with the stamped content.
            var csprojPath = RootDirectory / "src" / "DotNetMateTool" / "DotNetMateTool.csproj";
            var versionMatch = Regex.Match(File.ReadAllText(csprojPath), @"<Version>([^<]+)</Version>");
            var version = versionMatch.Success
                ? versionMatch.Groups[1].Value
                : ((IGitVersionComponent)this).SemVer;

            ITagTarget.RunGit("config user.email \"ci@flakroup.com\"");
            ITagTarget.RunGit("config user.name \"CI\"");
            ITagTarget.RunGit("add CHANGELOG.md src/DotNetMateTool/DotNetMateTool.csproj");
            ITagTarget.RunGit($"commit -m \"Release {version}: stamp CHANGELOG and Version\"");

            var serverUrl = Environment.GetEnvironmentVariable("CI_SERVER_URL");
            var projectPath = Environment.GetEnvironmentVariable("CI_PROJECT_PATH");
            var jobToken = Environment.GetEnvironmentVariable("CI_JOB_TOKEN");
            var branch = Environment.GetEnvironmentVariable("CI_COMMIT_BRANCH");

            if (!string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(projectPath) && !string.IsNullOrEmpty(branch))
            {
                var host = new Uri(serverUrl).Host;
                var scheme = new Uri(serverUrl).Scheme;
                var url = $"{scheme}://gitlab-ci-token:{jobToken}@{host}/{projectPath}.git";
                ITagTarget.RunGit($"push {url} HEAD:{branch}");
            }

            Log.Information("Committed and pushed stamped CHANGELOG.md for {Version}", version);
        });

    Target CI => _ => _
        .DependsOn(((ITestTarget)this).Test)
        .Executes(static () =>
        {
            Log.Information("CI pipeline completed");
        });

    public static int Main()
    {
        Bootstrap();
        return Execute<Build>(static x => ((ITestTarget)x).Test);
    }
}
