using DotNetMate.Core.JB;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace DotNetMate.Core.Tests;

public sealed class ReSharperServiceTests
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

    [Fact]
    public async Task OrderConfigAsync_WithUnsortedEntries_ShouldSortByXamlKey()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.DotSettings");
        const string xamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        const string wpfNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var doc = new XDocument(
            new XElement(XName.Get("ResourceDictionary", wpfNs),
                new XAttribute(XNamespace.Xmlns + "x", xamlNs),
                new XElement(XName.Get("String", wpfNs),
                    new XAttribute(XName.Get("Key", xamlNs), "/Default/CodeStyle/Naming/CSharp")),
                new XElement(XName.Get("String", wpfNs),
                    new XAttribute(XName.Get("Key", xamlNs), "/Default/CodeInspection/Highlighting")),
                new XElement(XName.Get("String", wpfNs),
                    new XAttribute(XName.Get("Key", xamlNs), "/Default/CodeStyle/CodeFormatting"))));

        doc.Save(tempPath);

        try
        {
            var file = new FileInfo(tempPath);

            // Act
            await ReSharperService.OrderConfigAsync(file, CancellationToken.None);

            // Assert
            var sorted = XDocument.Load(tempPath);
            var keys = sorted.Root.Elements()
                .Select(e => e.Attribute(XName.Get("Key", xamlNs))?.Value)
                .ToList();

            keys.Count.ShouldBe(3);
            keys[0].ShouldContain("CodeInspection");
            keys[1].ShouldContain("CodeStyle/CodeFormatting");
            keys[2].ShouldContain("CodeStyle/Naming");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task OrderConfigAsync_WithAlreadySortedEntries_ShouldPreserveOrder()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.DotSettings");
        const string xamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        const string wpfNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var doc = new XDocument(
            new XElement(XName.Get("ResourceDictionary", wpfNs),
                new XAttribute(XNamespace.Xmlns + "x", xamlNs),
                new XElement(XName.Get("String", wpfNs),
                    new XAttribute(XName.Get("Key", xamlNs), "/Default/A")),
                new XElement(XName.Get("String", wpfNs),
                    new XAttribute(XName.Get("Key", xamlNs), "/Default/B"))));

        doc.Save(tempPath);

        try
        {
            var file = new FileInfo(tempPath);

            // Act
            await ReSharperService.OrderConfigAsync(file, CancellationToken.None);

            // Assert
            var sorted = XDocument.Load(tempPath);
            var keys = sorted.Root.Elements()
                .Select(e => e.Attribute(XName.Get("Key", xamlNs))?.Value)
                .ToList();

            keys[0].ShouldBe("/Default/A");
            keys[1].ShouldBe("/Default/B");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task OrderConfigAsync_WithSingleEntry_ShouldNotThrow()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.DotSettings");
        const string xamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        const string wpfNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var doc = new XDocument(
            new XElement(XName.Get("ResourceDictionary", wpfNs),
                new XAttribute(XNamespace.Xmlns + "x", xamlNs),
                new XElement(XName.Get("String", wpfNs),
                    new XAttribute(XName.Get("Key", xamlNs), "/Default/Only"))));

        doc.Save(tempPath);

        try
        {
            var file = new FileInfo(tempPath);

            // Act & Assert - should not throw
            await ReSharperService.OrderConfigAsync(file, CancellationToken.None);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
