using DotNetMate.Core.JB;
using FEx.Agnostics.Abstractions.Flow;
using Shouldly;
using System.IO;
using Xunit;

namespace DotNetMate.Core.Tests;

public class ReSharperServiceTests
{
    [Fact]
    public void ValidateDotSettingsFile_WithNullFile_ShouldReturnError()
    {
        // Act
        Result<Error> result = ReSharperService.ValidateDotSettingsFile(null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ValidateDotSettingsFile_WithWrongExtension_ShouldReturnError()
    {
        // Arrange
        var file = new FileInfo("test.txt");

        // Act
        Result<Error> result = ReSharperService.ValidateDotSettingsFile(file);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain(".DotSettings");
    }

    [Fact]
    public void ValidateDotSettingsFile_WithCorrectExtension_ShouldReturnSuccess()
    {
        // Arrange - Create temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{System.Guid.NewGuid()}.DotSettings");
        File.WriteAllText(tempPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?><wpf:ResourceDictionary />");
        
        try
        {
            var file = new FileInfo(tempPath);

            // Act
            Result<Error> result = ReSharperService.ValidateDotSettingsFile(file);

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

