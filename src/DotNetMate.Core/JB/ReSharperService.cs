using FEx.Agnostics.Abstractions.Extensions;
using FEx.Agnostics.Abstractions.Flow;
using FEx.FileSystem;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DotNetMate.Core.JB;

public class ReSharperService
{
    public static async Task CleanCachesAsync(CancellationToken cancellationToken)
    {
        string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JetBrains");

        if (!Directory.Exists(basePath))
        {
            Log.Error($"Directory does not exist: {basePath}");

            return;
        }

        List<DirectoryInfo> solutionCacheDirs = await DirectoryWalker.SafeGetAllDirectoriesAsync(basePath, static d => d.Name.EndsWith("SolutionCaches", StringComparison.OrdinalIgnoreCase));

        await solutionCacheDirs.WithWhenAllAsync(ClearSolutionCaches, cancellationToken: cancellationToken);

        Log.Information("Done!");
    }

    public static async Task OrderConfigAsync(FileInfo settingsFile, CancellationToken cancellationToken)
    {
        string text = await File.ReadAllTextAsync(settingsFile.FullName, cancellationToken);
        var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);

        XElement root = doc.Root.Guard(nameof(XDocument.Root), "Invalid XML structure.");

        var sortedElements = root.Elements()
            .OrderBy(static e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value)
            .ToList();

        root.RemoveAll();

        foreach (XElement element in sortedElements)
            root.Add(element);

        doc.Save(settingsFile.FullName);

        Log.Information($"{settingsFile.Name} entries sorted successfully.");
    }

    public static Result<Error> ValidateDotSettingsFile(FileInfo fileInfo)
    {
        if (fileInfo is not null
            && fileInfo.Extension.IsEqual(".DotSettings"))
            return Result<Error>.Success;

        return (Error)"The file must exist and have a '.DotSettings' extension.";
    }

    private static void ClearSolutionCaches(DirectoryInfo dir)
    {
        bool isNotEmpty = IsDirectoryNotEmpty(dir);

        if (!isNotEmpty)
        {
            Log.Information($"Skipping empty folder: {dir.FullName}");

            return;
        }

        ClearDirectory(dir);
    }

    /// <summary>
    /// Checks if the specified directory contains at least one file or folder.
    /// </summary>
    private static bool IsDirectoryNotEmpty(DirectoryInfo dir) => dir.EnumerateFileSystemInfos().Any();

    /// <summary>
    /// Removes all files and subdirectories from the specified folder,
    /// but does NOT remove the folder itself.
    /// </summary>
    private static void ClearDirectory(DirectoryInfo dir)
    {
        Log.Information($"Clearing contents of: {dir.FullName}");

        foreach (FileInfo file in dir.EnumerateFiles())
            file.Delete();

        foreach (DirectoryInfo subDir in dir.EnumerateDirectories())
            subDir.Delete(true);
    }
}