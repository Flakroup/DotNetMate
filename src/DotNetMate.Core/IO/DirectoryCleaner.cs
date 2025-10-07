using FEx.Agnostics.Abstractions.Extensions;
using FEx.Core.Collections.Concurrent;
using FEx.FileSystem;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetMate.Core.IO;

public class DirectoryCleaner
{
    protected static ConcurrentHashSet<string> SystemDefaultFiles { get; }

    static DirectoryCleaner()
    {
        SystemDefaultFiles = ["desktop.ini", ".DS_Store", "Thumbs.db"];
    }

    public static async Task CleanAsync(DirectoryInfo targetFolder, CancellationToken cancellationToken)
    {
        targetFolder.Guard(nameof(targetFolder));

        Log.Information("Scanning directory: {TargetFolder}", targetFolder.FullName);

        List<DirectoryInfo> foldersToDelete = await GetFoldersToDeleteAsync(targetFolder);
        Log.Information("Found {FolderCount} folders to delete", foldersToDelete.Count);
        List<FileInfo> filesToDelete = GetFilesToDelete(targetFolder);
        Log.Information("Found {FileCount} files to delete", filesToDelete.Count);

        Log.Information("Sorting folders");
        List<DirectoryInfo> topLevelFolders = foldersToDelete.GetTopLevelFolders(true);
        Log.Information("Filtered {TopLevelCount} top-level folders to delete", topLevelFolders.Count);

        Log.Information("Deleting...");

        bool[] filesResults = await filesToDelete.WithWhenAllAsync(static file => file.SafeDelete(true),
            cancellationToken: cancellationToken);

        bool[] results = await topLevelFolders.WithWhenAllAsync(static folder => folder.SafeDelete(true, true),
            cancellationToken: cancellationToken);

        await RemoveEmptyDirectoriesAsync(targetFolder, false, cancellationToken);

        Log.Information("Deletion process completed");

        if (results.Any(static result => !result))
            await LogRemainingFoldersAsync(targetFolder);

        if (filesResults.Any(static result => !result))
            LogRemainingFiles(targetFolder);
    }

    public static async Task RemoveEmptyDirectoriesAsync(DirectoryInfo targetFolder,
                                                         bool ignoreSystemDefaultFiles,
                                                         CancellationToken cancellationToken)
    {
        targetFolder.Guard(nameof(targetFolder));

        if (!targetFolder.Exists)
        {
            Log.Debug("Directory does not exist: {DirectoryPath}", targetFolder.FullName);

            return;
        }

        Func<IReadOnlyCollection<FileSystemInfo>, bool> predicate = ignoreSystemDefaultFiles
            ? ContainsOnlySystemDefaultFiles
            : null;

        Log.Debug("Searching for empty directories...");

        List<DirectoryInfo> leafs =
            await targetFolder.SafeGetLeafDirectoriesAsync(directory => directory.IsEmpty(predicate));

        if (leafs.Count == 0)
            Log.Debug("No empty directories found");

        List<DirectoryInfo> deletedLeafs = [];

        while (leafs.Count > 0)
        {
            DirectoryInfo[] parents = await leafs.WithWhenAllAsync(
                leaf => DeleteAndReturnParent(leaf, true, true, predicate),
                cancellationToken: cancellationToken);

            deletedLeafs.AddRange(leafs);

            leafs = parents.Where(static parent => parent is not null)
                .DistinctBy(static parent => parent.FullName)
                .Where(leaf => leaf.IsEmpty(predicate))
                .ToList();
        }

        Log.Debug("--- SUMMARY ---");
        List<DirectoryInfo> topLeafs = deletedLeafs.GetTopLevelFolders();

        foreach (DirectoryInfo leaf in topLeafs)
            Log.Debug("Deleted directory: {DirectoryPath}", leaf.FullName);
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
            Log.Debug("Remaining folder: {FolderPath}", folder.FullName);
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
            Log.Debug("Remaining file: {FilePath}", file.FullName);
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

    private static bool FileToCleanPredicate(FileInfo file) =>
        file.Extension is ".binlog"
        || file.Extension is ".csproj" && Path.GetFileNameWithoutExtension(file.Name).EndsWith("_wpftmp");

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