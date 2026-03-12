using LibGit2Sharp;
using Shouldly;
using System;
using System.IO;
using Xunit;

namespace GitLogVisualizer.Tests;

public sealed class CommitTagInfoTests
{
    [Fact]
    public void Constructor_WithNullTag_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(static () => new CommitTagInfo(null));
    }

    [Fact]
    public void Constructor_WithValidCommitTag_ShouldSetProperties()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"GitTest_{Guid.NewGuid()}");

        try
        {
            Repository.Init(tempPath);

            using var repo = new Repository(tempPath);

            var sig = new Signature("Test User", "test@test.com", DateTimeOffset.Now);
            CreateAndStageFile(repo, tempPath, "file.txt");
            var commit = repo.Commit("Initial commit", sig, sig);
            var tag = repo.ApplyTag("v1.0.0", commit.Sha);

            // Act
            var tagInfo = new CommitTagInfo(tag);

            // Assert
            tagInfo.Tag.ShouldBe(tag);
            tagInfo.Commit.ShouldBe(commit);
            tagInfo.When.ShouldBe(commit.Author.When);
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
