using FEx.Building;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using System;
using System.Diagnostics.CodeAnalysis;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "AllUnderscoreLocalParameterName")]
class Build : FExBuild, ITagTarget
{
    string IPackTarget.PackSolution => (RootDirectory / "src" / "DotNetMateTool" / "DotNetMateTool.csproj").ToString();

    AbsolutePath TestResultsDirectory => RootDirectory / "artifacts" / "test-results";

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

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => GetRestoreSettings(s, Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => GetBuildSettings(s, Solution));
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Triggers(Benchmark)
        .Executes(() =>
        {
            TestResultsDirectory.CreateOrCleanDirectory();

            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .SetResultsDirectory(TestResultsDirectory)
                .SetLoggers("trx"));
        });

    Target Benchmark => _ => _
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

    Target CI => _ => _
        .DependsOn(Test)
        .Executes(static () =>
        {
            Log.Information("CI pipeline completed");
        });

    public static int Main()
    {
        Bootstrap();
        return Execute<Build>(static x => x.Test);
    }
}
