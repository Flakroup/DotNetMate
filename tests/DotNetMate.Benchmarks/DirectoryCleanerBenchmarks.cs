using BenchmarkDotNet.Attributes;
using DotNetMate.Core.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetMate.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class DirectoryCleanerBenchmarks
{
    private const int FileCount = 100;
    private const int DirectoryCount = 10;
    private DirectoryInfo _tempDir;

    [GlobalSetup]
    public void Setup()
    {
        // Create a temp directory structure for benchmarking
        string tempPath = Path.Combine(Path.GetTempPath(), $"DotNetMateBench_{Guid.NewGuid()}");
        _tempDir = Directory.CreateDirectory(tempPath);

        // Create some bin/obj directories
        for (var i = 0; i < DirectoryCount; i++)
        {
            DirectoryInfo binDir = _tempDir.CreateSubdirectory($"Project{i}/bin/Debug");
            DirectoryInfo objDir = _tempDir.CreateSubdirectory($"Project{i}/obj/Debug");

            // Add files
            for (var j = 0; j < FileCount / DirectoryCount; j++)
            {
                File.WriteAllText(Path.Combine(binDir.FullName, $"file{j}.dll"), "test content");
                File.WriteAllText(Path.Combine(objDir.FullName, $"file{j}.obj"), "test content");
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_tempDir?.Exists == true)
            _tempDir.Delete(true);
    }

    [Benchmark]
    public async Task CleanAsync()
    {
        await DirectoryCleaner.CleanAsync(_tempDir, CancellationToken.None);

        // Recreate for next iteration
        Setup();
    }

    [Benchmark]
    public async Task RemoveEmptyDirectoriesAsync()
    {
        DirectoryInfo emptyDir = _tempDir.CreateSubdirectory("EmptyTest");
        emptyDir.CreateSubdirectory("Level1/Level2/Level3");

        await DirectoryCleaner.RemoveEmptyDirectoriesAsync(_tempDir, false, CancellationToken.None);
    }
}