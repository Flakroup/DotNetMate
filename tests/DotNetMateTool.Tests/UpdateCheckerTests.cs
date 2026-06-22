using Shouldly;
using Xunit;

namespace DotNetMateTool.Tests;

public sealed class UpdateCheckerTests
{
    [Fact]
    public void ParseLatestVersion_WithMultipleStableVersions_ReturnsHighest()
    {
        // Arrange
        const string json = """{"versions":["1.0.0","1.0.1","1.2.0","1.1.5"]}""";

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
        const string json = """{"versions":["1.0.0","1.1.0","2.0.0-beta.1","2.0.0-rc.1"]}""";

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
        const string json = """{"versions":["1.0.0-alpha","2.0.0-beta","3.0.0-rc"]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseLatestVersion_WithEmptyVersionsList_ReturnsNull()
    {
        // Arrange
        const string json = """{"versions":[]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseLatestVersion_WithMissingVersionsProperty_ReturnsNull()
    {
        // Arrange
        const string json = """{"other":"value"}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParseLatestVersion_WithSingleVersion_ReturnsThatVersion()
    {
        // Arrange
        const string json = """{"versions":["1.5.3"]}""";

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
        const string json = """{"versions":["0.9.0","1.0.0","1.1.0-preview.1","1.0.5"]}""";

        // Act
        var result = UpdateChecker.ParseLatestVersion(json);

        // Assert
        result.ShouldNotBeNull();
        result.ToString().ShouldBe("1.0.5");
    }

    // ── StripBuildMetadata ──────────────────────────────────────────────

    [Theory]
    [InlineData("0.1.8+Branch.main.Sha.abc123", "0.1.8")]
    [InlineData("1.2.3+metadata", "1.2.3")]
    [InlineData("0.1.8", "0.1.8")]
    [InlineData(null, null)]
    public void StripBuildMetadata_RemovesSuffixAfterPlus(string input, string expected)
    {
        UpdateChecker.StripBuildMetadata(input).ShouldBe(expected);
    }

    // ── NuGet URL ───────────────────────────────────────────────────────

    [Fact]
    public void NuGetUrl_UsesLowercasePackageId()
    {
        UpdateChecker.NuGetUrl.ShouldBe("https://api.nuget.org/v3-flatcontainer/dotnetmatetool/index.json");
    }
}
