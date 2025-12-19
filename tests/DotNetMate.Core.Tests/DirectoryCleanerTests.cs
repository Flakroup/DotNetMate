using DotNetMate.Core.IO;
using Shouldly;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNetMate.Core.Tests;

public class DirectoryCleanerTests
{
    [Fact]
    public async Task CleanAsync_WithNullDirectory_ShouldThrow()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await DirectoryCleaner.CleanAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEmptyDirectoriesAsync_WithNullDirectory_ShouldThrow()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await DirectoryCleaner.RemoveEmptyDirectoriesAsync(null, false, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveEmptyDirectoriesAsync_WithNonExistentDirectory_ShouldCompleteWithoutError()
    {
        // Arrange
        var nonExistentDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        // Act & Assert - Should complete without throwing
        await DirectoryCleaner.RemoveEmptyDirectoriesAsync(nonExistentDir, false, CancellationToken.None);
    }

    [Fact]
    public async Task CleanAsync_WithEmptyTempDirectory_ShouldCompleteSuccessfully()
    {
        // Arrange
        DirectoryInfo tempDir =
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var dirInfo = new DirectoryInfo(tempDir.FullName);

            // Act
            await DirectoryCleaner.CleanAsync(dirInfo, CancellationToken.None);

            // Assert
            dirInfo.Exists.ShouldBeTrue();
        }
        finally
        {
            // Cleanup
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithBinAndObjFolders_ShouldDeleteThem()
    {
        // Arrange
        DirectoryInfo tempDir =
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            DirectoryInfo binDir = tempDir.CreateSubdirectory("bin");
            DirectoryInfo objDir = tempDir.CreateSubdirectory("obj");

            // Create some files inside
            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "test.dll"), "test", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(objDir.FullName, "test.obj"), "test", TestContext.Current.CancellationToken);

            var dirInfo = new DirectoryInfo(tempDir.FullName);

            // Act
            await DirectoryCleaner.CleanAsync(dirInfo, CancellationToken.None);

            // Assert
            binDir.Exists.ShouldBeFalse();
            objDir.Exists.ShouldBeFalse();
            tempDir.Exists.ShouldBeTrue(); // Root should still exist
        }
        finally
        {
            // Cleanup
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }
}