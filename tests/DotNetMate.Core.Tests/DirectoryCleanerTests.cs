using DotNetMate.Core.Configuration;
using DotNetMate.Core.IO;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNetMate.Core.Tests;

public sealed class DirectoryCleanerTests
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
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

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
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var binDir = tempDir.CreateSubdirectory("bin");
            var objDir = tempDir.CreateSubdirectory("obj");

            // Create some files inside
            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "test.dll"),
                "test",
                TestContext.Current.CancellationToken);

            await File.WriteAllTextAsync(Path.Combine(objDir.FullName, "test.obj"),
                "test",
                TestContext.Current.CancellationToken);

            var dirInfo = new DirectoryInfo(tempDir.FullName);

            // Act
            await DirectoryCleaner.CleanAsync(dirInfo, CancellationToken.None);

            // Assert
            binDir.Refresh();
            objDir.Refresh();
            binDir.Exists.ShouldBeFalse();
            objDir.Exists.ShouldBeFalse();
            tempDir.Exists.ShouldBeTrue();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithNestedBinObj_ShouldDeleteAll()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var projectA = tempDir.CreateSubdirectory("ProjectA");
            var projectB = tempDir.CreateSubdirectory("ProjectB");
            var binA = projectA.CreateSubdirectory("bin");
            var objB = projectB.CreateSubdirectory("obj");

            await File.WriteAllTextAsync(Path.Combine(binA.FullName, "a.dll"), "data",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(objB.FullName, "b.obj"), "data",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(projectA.FullName, "A.csproj"), "<Project/>",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(projectB.FullName, "B.csproj"), "<Project/>",
                TestContext.Current.CancellationToken);

            // Act
            await DirectoryCleaner.CleanAsync(new DirectoryInfo(tempDir.FullName), CancellationToken.None);

            // Assert
            binA.Refresh();
            objB.Refresh();
            binA.Exists.ShouldBeFalse();
            objB.Exists.ShouldBeFalse();
            projectA.Refresh();
            projectB.Refresh();
            projectA.Exists.ShouldBeTrue();
            projectB.Exists.ShouldBeTrue();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithVsDirectory_ShouldDeleteIt()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var vsDir = tempDir.CreateSubdirectory(".vs");
            await File.WriteAllTextAsync(Path.Combine(vsDir.FullName, "settings.json"), "{}",
                TestContext.Current.CancellationToken);

            // Act
            await DirectoryCleaner.CleanAsync(new DirectoryInfo(tempDir.FullName), CancellationToken.None);

            // Assert
            vsDir.Refresh();
            vsDir.Exists.ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithGitDirectory_ShouldProtectIt()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var gitDir = tempDir.CreateSubdirectory(".git");
            await File.WriteAllTextAsync(Path.Combine(gitDir.FullName, "HEAD"), "ref: refs/heads/main",
                TestContext.Current.CancellationToken);

            // Act
            await DirectoryCleaner.CleanAsync(new DirectoryInfo(tempDir.FullName), CancellationToken.None);

            // Assert
            gitDir.Refresh();
            gitDir.Exists.ShouldBeTrue();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithBinlogFiles_ShouldDeleteThem()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var binlogPath = Path.Combine(tempDir.FullName, "build.binlog");
            await File.WriteAllTextAsync(binlogPath, "binary log data",
                TestContext.Current.CancellationToken);

            // Act
            await DirectoryCleaner.CleanAsync(new DirectoryInfo(tempDir.FullName), CancellationToken.None);

            // Assert
            File.Exists(binlogPath).ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task RemoveEmptyDirectoriesAsync_WithEmptySubdirectories_ShouldDeleteThem()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var emptyA = tempDir.CreateSubdirectory("emptyA");
            var emptyB = tempDir.CreateSubdirectory("emptyB");
            var nonEmpty = tempDir.CreateSubdirectory("nonEmpty");
            await File.WriteAllTextAsync(Path.Combine(nonEmpty.FullName, "keep.txt"), "keep",
                TestContext.Current.CancellationToken);

            // Act
            var deleted = await DirectoryCleaner.RemoveEmptyDirectoriesAsync(
                new DirectoryInfo(tempDir.FullName), false, CancellationToken.None);

            // Assert
            emptyA.Refresh();
            emptyB.Refresh();
            nonEmpty.Refresh();
            deleted.ShouldBeGreaterThanOrEqualTo(2);
            emptyA.Exists.ShouldBeFalse();
            emptyB.Exists.ShouldBeFalse();
            nonEmpty.Exists.ShouldBeTrue();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task RemoveEmptyDirectoriesAsync_WithSystemFiles_ShouldTreatAsEmpty()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var dirWithSystemFile = tempDir.CreateSubdirectory("sysonly");
            await File.WriteAllTextAsync(Path.Combine(dirWithSystemFile.FullName, "desktop.ini"), "[.ShellClassInfo]",
                TestContext.Current.CancellationToken);

            // Act
            var deleted = await DirectoryCleaner.RemoveEmptyDirectoriesAsync(
                new DirectoryInfo(tempDir.FullName), true, CancellationToken.None);

            // Assert
            dirWithSystemFile.Refresh();
            deleted.ShouldBeGreaterThanOrEqualTo(1);
            dirWithSystemFile.Exists.ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task RemoveEmptyDirectoriesAsync_WithGitDirectory_ShouldProtectIt()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var gitDir = tempDir.CreateSubdirectory(".git");
            var refsDir = gitDir.CreateSubdirectory("refs");

            // Act
            await DirectoryCleaner.RemoveEmptyDirectoriesAsync(
                new DirectoryInfo(tempDir.FullName), false, CancellationToken.None);

            // Assert
            gitDir.Refresh();
            gitDir.Exists.ShouldBeTrue();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithCustomDirectories_ShouldDeleteThem()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var packagesDir = tempDir.CreateSubdirectory("packages");
            var artifactsDir = tempDir.CreateSubdirectory("artifacts");
            var srcDir = tempDir.CreateSubdirectory("src");

            await File.WriteAllTextAsync(Path.Combine(packagesDir.FullName, "pkg.nupkg"), "data",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(artifactsDir.FullName, "out.dll"), "data",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(srcDir.FullName, "Program.cs"), "code",
                TestContext.Current.CancellationToken);

            var config = new CleanConfig { CustomDirectories = ["packages", "artifacts"] };

            // Act
            await DirectoryCleaner.CleanAsync(new DirectoryInfo(tempDir.FullName), config, CancellationToken.None);

            // Assert
            packagesDir.Refresh();
            artifactsDir.Refresh();
            srcDir.Refresh();
            packagesDir.Exists.ShouldBeFalse();
            artifactsDir.Exists.ShouldBeFalse();
            srcDir.Exists.ShouldBeTrue();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithExcludePatterns_ShouldSkipMatching()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var projectDir = tempDir.CreateSubdirectory("MyProject");
            var binDir = projectDir.CreateSubdirectory("bin");

            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "app.dll"), "data",
                TestContext.Current.CancellationToken);

            var config = new CleanConfig { ExcludePatterns = ["MyProject"] };

            // Act
            await DirectoryCleaner.CleanAsync(new DirectoryInfo(tempDir.FullName), config, CancellationToken.None);

            // Assert - bin inside MyProject should be preserved
            binDir.Refresh();
            binDir.Exists.ShouldBeTrue();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithNullConfig_ShouldBehaveAsDefault()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var binDir = tempDir.CreateSubdirectory("bin");
            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "test.dll"), "data",
                TestContext.Current.CancellationToken);

            // Act
            await DirectoryCleaner.CleanAsync(new DirectoryInfo(tempDir.FullName), null, CancellationToken.None);

            // Assert
            binDir.Refresh();
            binDir.Exists.ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_CustomDirectories_ShouldNotAffectDefaultBehavior()
    {
        // Arrange
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

        try
        {
            var binDir = tempDir.CreateSubdirectory("bin");
            var customDir = tempDir.CreateSubdirectory("dist");

            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "app.dll"), "data",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(customDir.FullName, "bundle.js"), "data",
                TestContext.Current.CancellationToken);

            var config = new CleanConfig { CustomDirectories = ["dist"] };

            // Act
            await DirectoryCleaner.CleanAsync(new DirectoryInfo(tempDir.FullName), config, CancellationToken.None);

            // Assert - both default (bin) and custom (dist) should be deleted
            binDir.Refresh();
            customDir.Refresh();
            binDir.Exists.ShouldBeFalse();
            customDir.Exists.ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithLinkedWorktree_ShouldSkipBinObj()
    {
        var tempDir = CreateTestDir();

        try
        {
            var worktree = CreateFakeWorktree(tempDir, "wt-feature", "../.git/worktrees/wt-feature");
            var binDir = worktree.CreateSubdirectory("bin");
            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "app.dll"), "data",
                TestContext.Current.CancellationToken);

            await DirectoryCleaner.CleanAsync(tempDir, CancellationToken.None);

            binDir.Refresh();
            binDir.Exists.ShouldBeTrue();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithLinkedWorktreeAndIncludeFlag_ShouldCleanBinObj()
    {
        var tempDir = CreateTestDir();

        try
        {
            var worktree = CreateFakeWorktree(tempDir, "wt-feature", "../.git/worktrees/wt-feature");
            var binDir = worktree.CreateSubdirectory("bin");
            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "app.dll"), "data",
                TestContext.Current.CancellationToken);

            await DirectoryCleaner.CleanAsync(tempDir, null, CancellationToken.None, includeWorktrees: true);

            binDir.Refresh();
            binDir.Exists.ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithSubmodule_ShouldCleanBinObj()
    {
        var tempDir = CreateTestDir();

        try
        {
            var submodule = CreateFakeWorktree(tempDir, "sub", "../.git/modules/sub");
            var binDir = submodule.CreateSubdirectory("bin");
            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "lib.dll"), "data",
                TestContext.Current.CancellationToken);

            await DirectoryCleaner.CleanAsync(tempDir, CancellationToken.None);

            binDir.Refresh();
            binDir.Exists.ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithSubmoduleInsideWorktree_ShouldCleanBinObj()
    {
        var tempDir = CreateTestDir();

        try
        {
            // gitdir points to <main>/.git/worktrees/<wt>/modules/<sub> - last segment is /modules/, so it is a submodule, not a worktree.
            var submodule = CreateFakeWorktree(tempDir, "sub", "../.git/worktrees/wt-x/modules/sub");
            var binDir = submodule.CreateSubdirectory("bin");
            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "lib.dll"), "data",
                TestContext.Current.CancellationToken);

            await DirectoryCleaner.CleanAsync(tempDir, CancellationToken.None);

            binDir.Refresh();
            binDir.Exists.ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithEphemeralClaudeWorktree_ShouldCleanBinObj()
    {
        var tempDir = CreateTestDir();

        try
        {
            var claudeDir = tempDir.CreateSubdirectory(".claude").CreateSubdirectory("worktrees");
            var ephemeral = CreateFakeWorktree(claudeDir, "PM3-1991", "../../.git/worktrees/PM3-1991");
            var binDir = ephemeral.CreateSubdirectory("bin");
            await File.WriteAllTextAsync(Path.Combine(binDir.FullName, "agent.dll"), "data",
                TestContext.Current.CancellationToken);

            await DirectoryCleaner.CleanAsync(tempDir, CancellationToken.None);

            binDir.Refresh();
            binDir.Exists.ShouldBeFalse();
        }
        finally
        {
            if (tempDir.Exists)
                tempDir.Delete(true);
        }
    }

    private static DirectoryInfo CreateTestDir() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"DotNetMateTest_{Guid.NewGuid()}"));

    private static DirectoryInfo CreateFakeWorktree(DirectoryInfo parent, string name, string gitdirTarget)
    {
        var dir = parent.CreateSubdirectory(name);
        File.WriteAllText(Path.Combine(dir.FullName, ".git"), $"gitdir: {gitdirTarget}");

        return dir;
    }
}
