using LibGit2Sharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GitLogVisualizer.Tests;

public sealed class GitLogServiceTests
{
    [Fact]
    public async Task PrintGitLogAsync_WithRepoHavingNoUserCommits_ShouldNotThrow()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), $"GitLogTest_{Guid.NewGuid()}");
        var subRepo = Path.Combine(tempRoot, "repo1");

        try
        {
            Directory.CreateDirectory(tempRoot);
            Repository.Init(subRepo);

            // Empty repo: no branches, no commits — myLogs will be empty.
            // Pre-fix: PrintGitLogAsync throws InvalidOperationException on myLogs.First().

            // Act + Assert (no throw)
            await GitLogService.PrintGitLogAsync(
                new DirectoryInfo(tempRoot),
                DateTime.Now.AddMonths(-1),
                excluded: null,
                exportToJson: false,
                jsonToMerge: null,
                exportToCsv: false,
                tempo: false,
                CancellationToken.None);
        }
        finally
        {
            ForceDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task PrintGitLogAsync_WithNonExistentRoot_ShouldNotThrow()
    {
        // Arrange
        var nonExistent = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"DoesNotExist_{Guid.NewGuid()}"));

        // Act + Assert (no throw)
        await GitLogService.PrintGitLogAsync(
            nonExistent,
            DateTime.Now,
            excluded: null,
            exportToJson: false,
            jsonToMerge: null,
            exportToCsv: false,
            tempo: false,
            CancellationToken.None);
    }

    private static void ForceDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(path, true);
    }
}
