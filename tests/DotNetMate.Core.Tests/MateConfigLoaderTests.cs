using DotNetMate.Core.Configuration;
using Shouldly;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace DotNetMate.Core.Tests;

public sealed class MateConfigLoaderTests : IDisposable
{
    private readonly string _testRoot;

    public MateConfigLoaderTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "MateConfigTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }

    [Fact]
    public void Load_WithNoConfigFiles_ShouldReturnEmptyConfig()
    {
        // Act
        var config = MateConfigLoader.Load(_testRoot);

        // Assert
        config.ShouldNotBeNull();
        config.Clean.ShouldBeNull();
        config.GitLog.ShouldBeNull();
        config.ReSharper.ShouldBeNull();
    }

    [Fact]
    public void Load_WithConfigInCwd_ShouldLoadIt()
    {
        // Arrange
        var mateConfig = new MateConfig
        {
            Clean = new CleanConfig { CustomDirectories = ["packages", "artifacts"] }
        };

        WriteConfig(_testRoot, mateConfig);

        // Act
        var config = MateConfigLoader.Load(_testRoot);

        // Assert
        config.Clean.ShouldNotBeNull();
        config.Clean.CustomDirectories.ShouldBe(["packages", "artifacts"]);
    }

    [Fact]
    public void Load_WithConfigInParent_ShouldFindIt()
    {
        // Arrange
        var childDir = Path.Combine(_testRoot, "sub", "deep");
        Directory.CreateDirectory(childDir);

        var mateConfig = new MateConfig
        {
            GitLog = new GitLogConfig { DefaultAfter = "2026-01-01", Tempo = true }
        };

        WriteConfig(_testRoot, mateConfig);

        // Act
        var config = MateConfigLoader.Load(childDir);

        // Assert
        config.GitLog.ShouldNotBeNull();
        config.GitLog.DefaultAfter.ShouldBe("2026-01-01");
        config.GitLog.Tempo.ShouldBe(true);
    }

    [Fact]
    public void Load_CloserConfigOverridesSection_ShouldUseClosest()
    {
        // Arrange
        var childDir = Path.Combine(_testRoot, "project");
        Directory.CreateDirectory(childDir);

        var parentConfig = new MateConfig
        {
            Clean = new CleanConfig { CustomDirectories = ["packages"] },
            GitLog = new GitLogConfig { DefaultAfter = "2026-01-01" }
        };

        var childConfig = new MateConfig
        {
            Clean = new CleanConfig { CustomDirectories = ["dist", "output"] }
        };

        WriteConfig(_testRoot, parentConfig);
        WriteConfig(childDir, childConfig);

        // Act
        var config = MateConfigLoader.Load(childDir);

        // Assert - Clean section overridden by child
        config.Clean.CustomDirectories.ShouldBe(["dist", "output"]);

        // Assert - GitLog section inherited from parent
        config.GitLog.ShouldNotBeNull();
        config.GitLog.DefaultAfter.ShouldBe("2026-01-01");
    }

    [Fact]
    public void Load_WithInvalidJson_ShouldNotThrow()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, ".mate.json"), "{ invalid json }}}");

        // Act
        var config = MateConfigLoader.Load(_testRoot);

        // Assert
        config.ShouldNotBeNull();
    }

    [Fact]
    public void Load_WithPartialConfig_ShouldDeserializeAvailableSections()
    {
        // Arrange
        const string json = """{ "resharper": { "dotSettingsPaths": ["Global.DotSettings"] } }""";
        File.WriteAllText(Path.Combine(_testRoot, ".mate.json"), json);

        // Act
        var config = MateConfigLoader.Load(_testRoot);

        // Assert
        config.ReSharper.ShouldNotBeNull();
        config.ReSharper.DotSettingsPaths.ShouldBe(["Global.DotSettings"]);
        config.Clean.ShouldBeNull();
        config.GitLog.ShouldBeNull();
    }

    private static void WriteConfig(string directory, MateConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        File.WriteAllText(Path.Combine(directory, ".mate.json"), json);
    }
}
