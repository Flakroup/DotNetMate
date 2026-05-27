using DotNetMate.Core.Configuration;
using FEx.Agnostics.Abstractions.Enums;
using FEx.Agnostics.Abstractions.Extensions;
using FEx.Agnostics.Abstractions.Utilities;
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
        SystemDefaultFiles = ["desktop.ini", ".DS_Store", "Thumbs.db", "metadata.opf", "cover.jpg"];
        DirectoryWalker.Limit = 50;
    }

    public static Task CleanAsync(DirectoryInfo targetFolder,
                                  CancellationToken cancellationToken,
                                  bool includeWorktrees = false) =>
        CleanAsync(targetFolder, null, cancellationToken, includeWorktrees);

    public static async Task CleanAsync(DirectoryInfo targetFolder,
                                        CleanConfig config,
                                        CancellationToken cancellationToken,
                                        bool includeWorktrees = false)
    {
        targetFolder.Guard(nameof(targetFolder));

        Log.Information("Scanning directory: {TargetFolder}", targetFolder.FullName);

        var statistics = new CleanupStatistics();
        using var skippedWorktrees = new ConcurrentHashSet<string>();

        var foldersToDelete = await GetFoldersToDeleteAsync(targetFolder, config, includeWorktrees, skippedWorktrees);
        statistics.TotalFoldersFound = foldersToDelete.Count;
        Log.Information("Found {FolderCount} folders to delete", foldersToDelete.Count);

        var filesToDelete = GetFilesToDelete(targetFolder, config, includeWorktrees, skippedWorktrees);
        statistics.TotalFilesFound = filesToDelete.Count;
        Log.Information("Found {FileCount} files to delete", filesToDelete.Count);

        Log.Information("Sorting folders");
        var topLevelFolders = foldersToDelete.GetTopLevelFolders(true);
        Log.Information("Filtered {TopLevelCount} top-level folders to delete", topLevelFolders.Count);

        // Calculate sizes before deletion - use topLevelFolders to avoid double-counting
        Log.Information("Calculating sizes...");
        var filesSize = CalculateFilesSize(filesToDelete);
        var foldersSize = CalculateFoldersSize(topLevelFolders);
        statistics.TotalBytesDeleted = filesSize + foldersSize;

        Log.Information("Deleting...");

        var filesResults = await filesToDelete.WithWhenAllAsync(static file => file.SafeDelete(true),
            cancellationToken: cancellationToken);

        var results = await topLevelFolders.WithWhenAllAsync(static folder => folder.SafeDelete(true, true),
            cancellationToken: cancellationToken);

        statistics.FilesDeleted = filesResults.Count(static result => result);
        statistics.FoldersDeleted = results.Count(static result => result);

        var emptyDirsDeleted = await RemoveEmptyDirectoriesAsync(targetFolder, false, cancellationToken);
        statistics.EmptyDirectoriesDeleted = emptyDirsDeleted;

        statistics.SkippedWorktrees = skippedWorktrees.Count;

        Log.Information("Deletion process completed");

        LogSkippedWorktrees(skippedWorktrees);

        // Display statistics
        LogCleanupStatistics(statistics);

        if (results.Any(static result => !result))
            await LogRemainingFoldersAsync(targetFolder, config);

        if (filesResults.Any(static result => !result))
            LogRemainingFiles(targetFolder, config);
    }

    public static async Task<int> RemoveEmptyDirectoriesAsync(DirectoryInfo targetFolder,
                                                              bool ignoreSystemDefaultFiles,
                                                              CancellationToken cancellationToken)
    {
        targetFolder.Guard(nameof(targetFolder));

        if (!targetFolder.Exists)
        {
            Log.Debug("Directory does not exist: {DirectoryPath}", targetFolder.FullName);

            return 0;
        }

        Func<IReadOnlyCollection<FileSystemInfo>, bool> predicate = ignoreSystemDefaultFiles
            ? ContainsOnlySystemDefaultFiles
            : null;

        Log.Debug("Searching for empty directories...");

        var leafs = await targetFolder.SafeGetLeafDirectoriesAsync(directory =>
            !IsProtectedDirectory(directory) && directory.IsEmpty(predicate));

        if (leafs.Count == 0)
            Log.Debug("No empty directories found");

        List<DirectoryInfo> deletedLeafs = [];

        while (leafs.Count > 0)
        {
            var parents = await leafs.WithWhenAllAsync(leaf => DeleteAndReturnParent(leaf, true, true, predicate),
                cancellationToken: cancellationToken);

            deletedLeafs.AddRange(leafs);

            leafs = parents.Where(static parent => parent is not null)
                .DistinctBy(static parent => parent.FullName)
                .Where(leaf => !IsProtectedDirectory(leaf) && leaf.IsEmpty(predicate))
                .ToList();
        }

        Log.Debug("--- SUMMARY ---");
        var topLeafs = deletedLeafs.GetTopLevelFolders();

        foreach (var leaf in topLeafs)
            Log.Debug("Deleted directory: {DirectoryPath}", leaf.FullName);

        return deletedLeafs.Count;
    }

    private static bool IsProtectedDirectory(DirectoryInfo directory) =>
        directory.Name is ".git"
        || directory.FullName.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar);

    private static bool IsSkippableWorktree(DirectoryInfo dir,
                                            bool includeWorktrees,
                                            ConcurrentHashSet<string> skippedSink)
    {
        if (includeWorktrees)
            return false;

        if (dir.GetGitWorktreeKind() != GitWorktreeKind.Worktree)
            return false;

        // Ephemeral: any worktree under .claude/worktrees/ is a Claude Code agent isolation
        // dir - its bin/obj is leftover trash, so we clean it like a normal folder.
        var fullNorm = dir.FullName.Replace('\\', '/');

        if (fullNorm.Contains("/.claude/worktrees/"))
            return false;

        skippedSink?.Add(dir.FullName);

        return true;
    }

    private static void LogSkippedWorktrees(ConcurrentHashSet<string> skippedWorktrees)
    {
        if (skippedWorktrees.Count == 0)
            return;

        Log.Information(
            "Skipped {Count} linked git worktree(s) - pass --include-worktrees (-w) to clean them: {Paths}",
            skippedWorktrees.Count,
            skippedWorktrees);
    }

    private static bool ContainsOnlySystemDefaultFiles(IReadOnlyCollection<FileSystemInfo> directoryContents)
    {
        foreach (var directoryContent in directoryContents)
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

    private static async Task LogRemainingFoldersAsync(DirectoryInfo targetFolder, CleanConfig config = null)
    {
        var remainingFolders = await GetFoldersToDeleteAsync(targetFolder, config);

        if (remainingFolders.Count == 0)
        {
            Log.Information("All specified folders have been successfully deleted");

            return;
        }

        Log.Warning("WARNING: There are some folders left that could not be deleted:");

        foreach (var folder in remainingFolders)
            Log.Debug("Remaining folder: {FolderPath}", folder.FullName);
    }

    private static void LogRemainingFiles(DirectoryInfo targetFolder, CleanConfig config = null)
    {
        var remainingFiles = GetFilesToDelete(targetFolder, config);

        if (remainingFiles.Count == 0)
        {
            Log.Information("All specified files have been successfully deleted");

            return;
        }

        Log.Warning("WARNING: There are some files left that could not be deleted:");

        foreach (var file in remainingFiles)
            Log.Debug("Remaining file: {FilePath}", file.FullName);
    }

    private static async Task<List<DirectoryInfo>> GetFoldersToDeleteAsync(DirectoryInfo rootFolder,
                                                                          CleanConfig config = null,
                                                                          bool includeWorktrees = false,
                                                                          ConcurrentHashSet<string> skippedWorktrees = null)
    {
        if (!rootFolder.Exists)
            return [];

        bool Predicate(DirectoryInfo dir) => DirectoryToCleanPredicate(dir, config);

        bool SkipPredicate(DirectoryInfo dir) =>
            Predicate(dir)
            || IsProtectedDirectory(dir)
            || IsSkippableWorktree(dir, includeWorktrees, skippedWorktrees);

        return await rootFolder.SafeGetAllDirectoriesAsync(Predicate,
            skipRecursionPredicate: SkipPredicate);
    }

    private static List<FileInfo> GetFilesToDelete(DirectoryInfo rootFolder,
                                                   CleanConfig config = null,
                                                   bool includeWorktrees = false,
                                                   ConcurrentHashSet<string> skippedWorktrees = null)
    {
        if (!rootFolder.Exists)
            return [];

        bool SkipPredicate(DirectoryInfo dir) =>
            DirectoryToCleanPredicate(dir, config)
            || IsProtectedDirectory(dir)
            || IsSkippableWorktree(dir, includeWorktrees, skippedWorktrees);

        return rootFolder.SafeGetAllFiles(FileToCleanPredicate,
            skipDirectoryPredicate: SkipPredicate);
    }

    private static bool DirectoryToCleanPredicate(DirectoryInfo dir, CleanConfig config = null)
    {
        if (IsExcluded(dir, config))
            return false;

        if (dir.Name is "bin" or "obj" or ".vs" or ".tmp" or "TestResults"
            || dir.Name.EndsWith("Installer-cache", StringComparison.OrdinalIgnoreCase)
            || dir.Name is "temp" && dir.Parent?.Name is ".nuke")
            return true;

        return config?.CustomDirectories?.Contains(dir.Name, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static bool IsExcluded(DirectoryInfo dir, CleanConfig config) =>
        config?.ExcludePatterns?.Exists(pattern =>
            dir.FullName.Contains(pattern, StringComparison.OrdinalIgnoreCase)) == true;

    private static bool ShouldSkipRecursion(DirectoryInfo dir) =>
        DirectoryToCleanPredicate(dir) || IsProtectedDirectory(dir);

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

    private static long CalculateFilesSize(List<FileInfo> files)
    {
        long totalSize = 0;

        foreach (var file in files)
        {
            try
            {
                if (file.Exists)
                    totalSize += file.Length;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to get size for file: {FilePath}", file.FullName);
            }
        }

        return totalSize;
    }

    private static long CalculateFoldersSize(List<DirectoryInfo> folders)
    {
        long totalSize = 0;

        foreach (var folder in folders)
        {
            if (!folder.Exists)
                continue;

            try
            {
                var files = folder.GetFiles("*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    try
                    {
                        totalSize += file.Length;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to get size for file: {FilePath}", file.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to enumerate directory: {DirectoryPath}", folder.FullName);
            }
        }

        return totalSize;
    }

    private static void LogCleanupStatistics(CleanupStatistics statistics)
    {
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("                    CLEANUP STATISTICS                          ");
        Log.Information("═══════════════════════════════════════════════════════════════");
        Log.Information("Folders found:         {TotalFoldersFound}", statistics.TotalFoldersFound);
        Log.Information("Folders deleted:       {FoldersDeleted}", statistics.FoldersDeleted);
        Log.Information("Files found:           {TotalFilesFound}", statistics.TotalFilesFound);
        Log.Information("Files deleted:         {FilesDeleted}", statistics.FilesDeleted);
        Log.Information("Empty dirs deleted:    {EmptyDirectoriesDeleted}", statistics.EmptyDirectoriesDeleted);

        if (statistics.SkippedWorktrees > 0)
            Log.Information("Worktrees skipped:     {SkippedWorktrees}", statistics.SkippedWorktrees);

        Log.Information("───────────────────────────────────────────────────────────────");

        Log.Information("Total size deleted:    {TotalSize} ({TotalBytes:N0} bytes)",
            statistics.FormattedSize,
            statistics.TotalBytesDeleted);

        Log.Information("═══════════════════════════════════════════════════════════════");
    }

    public class CleanupStatistics
    {
        public int TotalFoldersFound { get; set; }
        public int TotalFilesFound { get; set; }
        public int FoldersDeleted { get; set; }
        public int FilesDeleted { get; set; }
        public long TotalBytesDeleted { get; set; }
        public int EmptyDirectoriesDeleted { get; set; }
        public int SkippedWorktrees { get; set; }

        public string FormattedSize =>
            FileLengthConverter.ConvertFileLengthToString(TotalBytesDeleted,
                LengthType.Bytes,
                LengthType.AutoDetect,
                2);
    }
}