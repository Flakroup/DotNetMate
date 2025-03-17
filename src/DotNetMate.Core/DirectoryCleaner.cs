using FEx.Common.Extensions;
using FEx.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMate.Core;

public class DirectoryCleaner
{
    public static async Task CleanAsync(DirectoryInfo targetFolder)
    {
        targetFolder.Guard(nameof(targetFolder));

        Log.Information($"Scanning\t{targetFolder}");

        List<DirectoryInfo> foldersToDelete = GetFoldersToDelete(targetFolder);
        Log.Information($"Found {foldersToDelete.Count} folders to delete.");
        List<FileInfo> filesToDelete = GetFilesToDelete(targetFolder);

        Log.Information("Sorting folders");
        List<DirectoryInfo> topLevelFolders = foldersToDelete.GetTopLevelFolders(true);
        Log.Information($"Filtered {topLevelFolders.Count} top-level folders to delete.");

        Log.Information("Deleting...");
        bool[] filesResults = await filesToDelete.RunWithWhenAllAsync(file => file.SafeDelete(true));
        bool[] results = await topLevelFolders.RunWithWhenAllAsync(folder => folder.SafeDelete(true, true));
        await RemoveEmptyDirectoriesAsync(targetFolder);

        Log.Information("Deletion process completed");

        if (results.Any(result => !result))
            LogRemainingFolders(targetFolder);

        if (filesResults.Any(result => !result))
            LogRemainingFiles(targetFolder);
    }

    public static async Task RemoveEmptyDirectoriesAsync(DirectoryInfo targetFolder)
    {
        Log.Debug("Searching for empty directories...");
        List<DirectoryInfo> leafs = targetFolder.SafeGetLeafDirectories(directory => directory.IsEmpty());

        if (leafs.Count == 0)
            Log.Debug("No empty directories found");

        while (leafs.Count > 0)
        {
            DirectoryInfo[] parents =
                await leafs.RunWithWhenAllAsync(leaf => DeleteAndReturnParent(leaf, logDeletions: true));

            leafs = parents.Where(parent => parent is not null)
                .DistinctBy(parent => parent.FullName)
                .Where(leaf => leaf.IsEmpty())
                .ToList();
        }
    }

    private static void LogRemainingFolders(DirectoryInfo targetFolder)
    {
        List<DirectoryInfo> remainingFolders = GetFoldersToDelete(targetFolder);

        if (remainingFolders.Count == 0)
        {
            Log.Information("All specified folders have been successfully deleted");

            return;
        }

        Log.Warning("WARNING: There are some folders left that could not be deleted:");

        foreach (DirectoryInfo folder in remainingFolders)
            Log.Debug(folder.FullName);
    }

    private static void LogRemainingFiles(DirectoryInfo targetFolder)
    {
        List<FileInfo> remainingFiles = GetFilesToDelete(targetFolder);

        if (remainingFiles.Count == 0)
        {
            Log.Information("All specified files have been successfully deleted");

            return;
        }

        Log.Warning("WARNING: There are some files left that could not be deleted:");

        foreach (FileInfo file in remainingFiles)
            Log.Debug(file.FullName);
    }

    /// <summary>
    /// Returns a list of all candidate folders to delete (bin, obj, .vs, .tmp,
    /// or any folder whose name ends with "Installer-cache").
    /// </summary>
    private static List<DirectoryInfo> GetFoldersToDelete(DirectoryInfo rootFolder) =>
        rootFolder.Exists
            ? rootFolder.SafeGetAllDirectories(DirectoryToCleanPredicate)
            : [];

    private static List<FileInfo> GetFilesToDelete(DirectoryInfo rootFolder) =>
        rootFolder.Exists
            ? rootFolder.SafeGetAllFiles(FileToCleanPredicate)
            : [];

    private static bool DirectoryToCleanPredicate(DirectoryInfo dir) =>
        dir.Name is "bin" or "obj" or ".vs" or ".tmp" or "TestResults"
        || dir.Name.EndsWith("Installer-cache", StringComparison.OrdinalIgnoreCase);

    private static bool FileToCleanPredicate(FileInfo file) => file.Extension is ".binlog";

    /// <summary>
    /// Attempts to delete a folder. Logs success or error.
    /// </summary>
    private static DirectoryInfo DeleteAndReturnParent(DirectoryInfo directory,
                                                       bool recursive = false,
                                                       bool logDeletions = false) =>
        directory.SafeDelete(recursive, logDeletions) && directory.Parent?.IsEmpty() == true
            ? directory.Parent
            : null;
}