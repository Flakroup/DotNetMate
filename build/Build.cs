using FEx.Building;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "AllUnderscoreLocalParameterName")]
class Build : FExBuild, ITagTarget, ITestTarget
{
    string IPackTarget.PackProject => (RootDirectory / "src" / "DotNetMateTool" / "DotNetMateTool.csproj").ToString();

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
        .Executes(() =>
        {
            var benchmarkProject = Solution.GetProject("DotNetMate.Benchmarks");

            if (IsLocalBuild)
                DotNet($"run --project {benchmarkProject} --configuration {Configuration} --no-build -- -f * --join");
            else
                DotNet($"run --project {benchmarkProject} --configuration {Configuration} --no-build -- -f * --join -j Dry");
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
        .Description("Stamps [Unreleased] in CHANGELOG.md with the current GitVersion before packing")
        .DependentFor(((IPackTarget)this).Pack)
        .OnlyWhenDynamic(() => IsServerBuild, "Skipping changelog stamp: not running on CI")
        .Executes(() =>
        {
            var changelogPath = RootDirectory / "CHANGELOG.md";

            if (!File.Exists(changelogPath))
            {
                Log.Information("CHANGELOG.md not found - skipping");
                return;
            }

            var content = File.ReadAllText(changelogPath);
            var version = ((IGitVersionComponent)this).SemVer;
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var updated = content.Replace("## [Unreleased]", $"## [{version}] - {date}");

            if (updated == content)
            {
                Log.Information("No [Unreleased] section in CHANGELOG.md - skipping");
                return;
            }

            File.WriteAllText(changelogPath, updated);
            Log.Information("Stamped CHANGELOG.md: [Unreleased] -> [{Version}] - {Date}", version, date);
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

            var version = ((IGitVersionComponent)this).SemVer;

            ITagTarget.RunGit("config user.email \"ci@flakroup.com\"");
            ITagTarget.RunGit("config user.name \"CI\"");
            ITagTarget.RunGit("add CHANGELOG.md");
            ITagTarget.RunGit($"commit -m \"Release {version}: stamp CHANGELOG\"");

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
