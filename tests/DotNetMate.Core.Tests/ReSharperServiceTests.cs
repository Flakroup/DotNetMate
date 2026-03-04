using DotNetMate.Core.JB;
using Shouldly;
using System;
using System.IO;
using Xunit;

namespace DotNetMate.Core.Tests;

public class ReSharperServiceTests
{
    [Fact]
    public void ValidateDotSettingsFile_WithNullFile_ShouldReturnError()
    {
        // Act
        var result = ReSharperService.ValidateDotSettingsFile(null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ValidateDotSettingsFile_WithWrongExtension_ShouldReturnError()
    {
        // Arrange
        var file = new FileInfo("test.txt");

        // Act
        var result = ReSharperService.ValidateDotSettingsFile(file);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain(".DotSettings");
    }

    [Fact]
    public void ValidateDotSettingsFile_WithCorrectExtension_ShouldReturnSuccess()
    {
        // Arrange - Create temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.DotSettings");
        File.WriteAllText(tempPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?><wpf:ResourceDictionary />");

        try
        {
            var file = new FileInfo(tempPath);

            // Act
            var result = ReSharperService.ValidateDotSettingsFile(file);

            // Assert
            result.IsSuccess.ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}