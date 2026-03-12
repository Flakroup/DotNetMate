using LibGit2Sharp;
using Shouldly;
using System;
using System.IO;
using Xunit;

namespace GitLogVisualizer.Tests;

public sealed class RepositoryInfoTests
{
    [Fact]
    public void GetRepositoryInformationForPath_WithNullDirectory_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(
            static () => RepositoryInfo.GetRepositoryInformationForPath(null));
    }

    [Fact]
    public void GetRepositoryInformationForPath_WithNonGitDirectory_ShouldThrow()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"NonGit_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempPath);

        try
        {
            // Act & Assert
            Should.Throw<InvalidOperationException>(
                () => RepositoryInfo.GetRepositoryInformationForPath(new DirectoryInfo(tempPath)));
        }
        finally
        {
            Directory.Delete(tempPath, true);
        }
    }

    [Fact]
    public void GetRepositoryInformationForPath_WithValidRepo_ShouldReturnInfo()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"GitTest_{Guid.NewGuid()}");

        try
        {
            Repository.Init(tempPath);

            using (var setupRepo = new Repository(tempPath))
            {
                var sig = new Signature("Test User", "test@test.com", DateTimeOffset.Now);
                setupRepo.Config.Set("user.name", "Test User");
                setupRepo.Config.Set("user.email", "test@test.com");

                CreateAndStageFile(setupRepo, tempPath, "readme.txt");
                setupRepo.Commit("Initial commit", sig, sig);

                var mainBranch = setupRepo.Branches["main"] ?? setupRepo.Branches["master"];

                if (mainBranch is null)
                {
                    var currentBranch = setupRepo.Head;
                    setupRepo.Branches.Rename(currentBranch, "main");
                }

                using (setupRepo.Network.Remotes.Add("origin", "https://example.com/test.git"))
                {
                }
            }

            // Act
            using var repoInfo = RepositoryInfo.GetRepositoryInformationForPath(new DirectoryInfo(tempPath));

            // Assert
            repoInfo.Name.ShouldBe(new DirectoryInfo(tempPath).Name);
            repoInfo.Repo.ShouldNotBeNull();
            repoInfo.Me.ShouldNotBeNull();
        }
        finally
        {
            ForceDeleteDirectory(tempPath);
        }
    }

    private static void CreateAndStageFile(Repository repo, string repoPath, string fileName)
    {
        File.WriteAllText(Path.Combine(repoPath, fileName), "content");
        Commands.Stage(repo, fileName);
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
