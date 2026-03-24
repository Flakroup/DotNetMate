using DotNetMateTool;
using Shouldly;
using Xunit;

namespace DotNetMateTool.Tests;

public sealed class UpdateCheckerTests
{
    [Fact]
    public void ParseLatestVersion_WithMultipleStableVersions_ReturnsHighest()
    {
        // Arrange
        var json = """{"versions":["1.0.0","1.0.1","1.2.0","1.1.5"]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldNotBeNull();
        result.ToString().ShouldBe("1.2.0");
    }

    [Fact]
    public void ParseLatestVersion_WithPrereleases_SkipsThemAndReturnsHighestStable()
    {
        // Arrange
        var json = """{"versions":["1.0.0","1.1.0","2.0.0-beta.1","2.0.0-rc.1"]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldNotBeNull();
        result.ToString().ShouldBe("1.1.0");
    }

    [Fact]
    public void ParseLatestVersion_WithAllPrereleases_ReturnsNull()
    {
        // Arrange
        var json = """{"versions":["1.0.0-alpha","2.0.0-beta","3.0.0-rc"]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseLatestVersion_WithEmptyVersionsList_ReturnsNull()
    {
        // Arrange
        var json = """{"versions":[]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseLatestVersion_WithMissingVersionsProperty_ReturnsNull()
    {
        // Arrange
        var json = """{"other":"value"}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseLatestVersion_WithSingleVersion_ReturnsThatVersion()
    {
        // Arrange
        var json = """{"versions":["1.5.3"]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldNotBeNull();
        result.ToString().ShouldBe("1.5.3");
    }

    [Fact]
    public void ParseLatestVersion_WithMixedStableAndPrerelease_ReturnsHighestStable()
    {
        // Arrange
        var json = """{"versions":["0.9.0","1.0.0","1.1.0-preview.1","1.0.5"]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldNotBeNull();
        result.ToString().ShouldBe("1.0.5");
    }
}
