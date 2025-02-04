using FEx.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMateTool;

public class ReSharperService
{
    public static async Task HandleAsync(bool clean)
    {
        if (!clean)
            return;

        string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JetBrains");

        if (!Directory.Exists(basePath))
        {
            Log.Error($"Directory does not exist: {basePath}");

            return;
        }

        IEnumerable<DirectoryInfo> allDirs = DirWalker.SafeGetAllDirectories(basePath);

        var solutionCacheDirs = allDirs
            .Where(d => d.Name.EndsWith("SolutionCaches", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await solutionCacheDirs.RunWithWhenAllAsync(ClearSolutionCaches);

        Log.Information("Done!");
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
    /// This mimics 'Remove-Item (Join-Path X '*') -Recurse -Force'.
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