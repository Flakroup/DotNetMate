using BenchmarkDotNet.Attributes;
using DotNetMate.Core.IO;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetMate.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class DirectoryCleanerBenchmarks
{
    private const int FileCount = 400;
    private const int DirectoryCount = 20;
    private DirectoryInfo _cleanDir;
    private DirectoryInfo _removeEmptyDir;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cleanDir = CreateFreshDirectory(Path.Combine(Path.GetTempPath(), "DotNetMateBench_Clean"));
        _removeEmptyDir = CreateFreshDirectory(Path.Combine(Path.GetTempPath(), "DotNetMateBench_RemoveEmpty"));
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        // Use Directory.Exists (not DirectoryInfo.Exists) - DirectoryInfo caches state
        if (Directory.Exists(_cleanDir?.FullName))
            Directory.Delete(_cleanDir.FullName, true);
        if (Directory.Exists(_removeEmptyDir?.FullName))
            Directory.Delete(_removeEmptyDir.FullName, true);
    }

    [IterationSetup(Target = nameof(CleanAsync))]
    public void SetupClean()
    {
        // CleanAsync bubbles RemoveEmptyDirectories up to the root, deleting _cleanDir itself
        _cleanDir = CreateFreshDirectory(_cleanDir.FullName);

        for (var i = 0; i < DirectoryCount; i++)
        {
            var binDir = _cleanDir.CreateSubdirectory($"Project{i}/bin/Debug");
            var objDir = _cleanDir.CreateSubdirectory($"Project{i}/obj/Debug");

            for (var j = 0; j < FileCount / DirectoryCount; j++)
            {
                File.WriteAllText(Path.Combine(binDir.FullName, $"file{j}.dll"), "test content");
                File.WriteAllText(Path.Combine(objDir.FullName, $"file{j}.obj"), "test content");
            }
        }
    }

    [IterationSetup(Target = nameof(RemoveEmptyDirectoriesAsync))]
    public void SetupRemoveEmpty()
    {
        // Same issue: RemoveEmptyDirectoriesAsync deletes _removeEmptyDir itself when it empties
        _removeEmptyDir = CreateFreshDirectory(_removeEmptyDir.FullName);
        _removeEmptyDir.CreateSubdirectory("Level1/Level2/Level3");
    }

    [Benchmark]
    public async Task CleanAsync()
    {
        await DirectoryCleaner.CleanAsync(_cleanDir, CancellationToken.None);
    }

    [Benchmark]
    public async Task RemoveEmptyDirectoriesAsync()
    {
        await DirectoryCleaner.RemoveEmptyDirectoriesAsync(_removeEmptyDir, false, CancellationToken.None);
    }

    private static DirectoryInfo CreateFreshDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);

        return Directory.CreateDirectory(path);
    }
}
