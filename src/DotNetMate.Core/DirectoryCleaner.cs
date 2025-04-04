using FEx.Basics.Collections.Concurrent;
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
    protected static ConcurrentHashSet<string> SystemDefaultFiles { get; }

    static DirectoryCleaner()
    {
        SystemDefaultFiles = ["desktop.ini", ".DS_Store", "Thumbs.db"];
    }

    public static async Task CleanAsync(DirectoryInfo targetFolder)
    {
        targetFolder.Guard(nameof(targetFolder));

        Log.Information($"Scanning\t{targetFolder}");

        List<DirectoryInfo> foldersToDelete = await GetFoldersToDeleteAsync(targetFolder);
        Log.Information($"Found {foldersToDelete.Count} folders to delete.");
        List<FileInfo> filesToDelete = GetFilesToDelete(targetFolder);

        Log.Information("Sorting folders");
        List<DirectoryInfo> topLevelFolders = foldersToDelete.GetTopLevelFolders(true);
        Log.Information($"Filtered {topLevelFolders.Count} top-level folders to delete.");

        Log.Information("Deleting...");
        bool[] filesResults = await filesToDelete.RunWithWhenAllAsync(file => file.SafeDelete(true));
        bool[] results = await topLevelFolders.RunWithWhenAllAsync(folder => folder.SafeDelete(true, true));
        await RemoveEmptyDirectoriesAsync(targetFolder, false);

        Log.Information("Deletion process completed");

        if (results.Any(result => !result))
            await LogRemainingFoldersAsync(targetFolder);

        if (filesResults.Any(result => !result))
            LogRemainingFiles(targetFolder);
    }

    public static async Task RemoveEmptyDirectoriesAsync(DirectoryInfo targetFolder, bool ignoreSystemDefaultFiles)
    {
        Func<IReadOnlyCollection<FileSystemInfo>, bool> predicate = ignoreSystemDefaultFiles
            ? ContainsOnlySystemDefaultFiles
            : null;

        Log.Debug("Searching for empty directories...");
        List<DirectoryInfo> leafs = await targetFolder.SafeGetLeafDirectoriesAsync(directory => directory.IsEmpty(predicate));

        if (leafs.Count == 0)
            Log.Debug("No empty directories found");

        List<DirectoryInfo> deletedLeafs = [];

        while (leafs.Count > 0)
        {
            DirectoryInfo[] parents = await leafs.RunWithWhenAllAsync(leaf =>
                DeleteAndReturnParent(leaf, recursive: true, logDeletions: true, predicate: predicate));

            deletedLeafs.AddRange(leafs);

            leafs = parents.Where(parent => parent is not null)
                .DistinctBy(parent => parent.FullName)
                .Where(leaf => leaf.IsEmpty(predicate))
                .ToList();
        }

        Log.Debug("--- SUMMARY ---");
        List<DirectoryInfo> topLeafs = deletedLeafs.GetTopLevelFolders();

        foreach (DirectoryInfo leaf in topLeafs)
            Log.Debug(leaf.FullName);
    }

    private static bool ContainsOnlySystemDefaultFiles(IReadOnlyCollection<FileSystemInfo> directoryContents)
    {
        foreach (FileSystemInfo directoryContent in directoryContents)
        {
            switch (directoryContent)
            {
                case DirectoryInfo:
                case FileInfo file when !SystemDefaultFiles.Contains(file.Name):
                    return false;
            }
        }

        return true;
    }

    private static async Task LogRemainingFoldersAsync(DirectoryInfo targetFolder)
    {
        List<DirectoryInfo> remainingFolders = await GetFoldersToDeleteAsync(targetFolder);

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
    private static async Task<List<DirectoryInfo>> GetFoldersToDeleteAsync(DirectoryInfo rootFolder) =>
        rootFolder.Exists
            ? await rootFolder.SafeGetAllDirectoriesAsync(DirectoryToCleanPredicate)
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
                                                       bool logDeletions = false,
                                                       Func<IReadOnlyCollection<FileSystemInfo>, bool> predicate =
                                                           null) =>
        directory.SafeDelete(recursive, logDeletions) && directory.Parent?.IsEmpty(predicate) == true
            ? directory.Parent
            : null;
}