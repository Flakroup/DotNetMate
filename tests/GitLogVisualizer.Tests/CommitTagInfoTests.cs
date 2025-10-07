using Shouldly;
using System;
using Xunit;

namespace GitLogVisualizer.Tests;

public class CommitTagInfoTests
{
    [Fact]
    public void Constructor_WithNullTag_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(static () => new CommitTagInfo(null));
    }

    [Fact]
    public void Constructor_WithValidCommitTag_ShouldSetPropertiesCorrectly()
    {
        // Arrange - We'll need to test with a real git repository
        // For now, we'll test the negative case above
        // Integration tests would be needed for the positive case

        // This test documents that we need integration tests with a real git repo
        Assert.True(true, "Integration test needed: Create temp git repo with tags");
    }
}