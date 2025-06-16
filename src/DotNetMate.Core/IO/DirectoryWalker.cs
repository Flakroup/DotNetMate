using FEx.Abstractions;
using FEx.Asyncx.Helpers;
using FEx.Basics.Collections.Concurrent;
using FEx.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMate.Core.IO;

public static class DirectoryWalker
{
    public static uint Limit
    {
        get => Queue.ConcurrencyLimit;
        set => Queue.ConcurrencyLimit = value;
    }

    private static ConcurrentHashSet<string> ErrorPaths { get; }
    private static EnumerationOptions DefaultOptions { get; }
    private static AsyncProcessingQueue Queue { get; }

    static DirectoryWalker()
    {
        ErrorPaths = [];
        Queue = new();

        DefaultOptions = new()
        {
            MatchType = MatchType.Win32,
            AttributesToSkip = 0,
            IgnoreInaccessible = false
        };
    }

    /// <summary>
    /// Recursively gets all subdirectories from a root directory, ignoring
    /// any directories that throw an UnauthorizedAccessException or other IO exceptions.
    /// </summary>
    public static async Task<List<DirectoryInfo>> SafeGetAllDirectoriesAsync(string rootPath,
                                                                             DirectoryFilterDelegate predicate = null,
                                                                             string searchPattern = "*",
                                                                             EnumerationOptions options = null) =>
        await new DirectoryInfo(rootPath).SafeGetAllDirectoriesAsync(predicate, searchPattern, options);

    /// <summary>
    /// Recursively gets all subdirectories from a root directory, ignoring
    /// any directories that throw an UnauthorizedAccessException or other IO exceptions.
    /// </summary>
    public static async Task<List<DirectoryInfo>> SafeGetAllDirectoriesAsync(this DirectoryInfo root,
                                                                             DirectoryFilterDelegate predicate = null,
                                                                             string searchPattern = "*",
                                                                             EnumerationOptions options = null)
    {
        if (!root.Exists)
            throw new DirectoryNotFoundException($"Specified path doesn't exist: {root.FullName}");

        bool hasFilter = predicate is not null;
        options ??= DefaultOptions;

        return await GetDirectoriesAsync(root, hasFilter, predicate, searchPattern, options);
    }

    public static List<FileInfo> SafeGetAllFiles(this DirectoryInfo root,
                                                 FileFilterDelegate predicate = null,
                                                 string searchPattern = "*",
                                                 EnumerationOptions options = null)
    {
        if (!root.Exists)
            throw new DirectoryNotFoundException($"Specified path doesn't exist: {root.FullName}");

        bool hasFilter = predicate is not null;
        options ??= DefaultOptions;

        var stack = new Stack<DirectoryInfo>();
        stack.Push(root);

        var result = new List<FileInfo>();

        while (stack.Count > 0)
        {
            DirectoryInfo current = stack.Pop();

            if (current.IsErrorPath())
                continue;

            try
            {
                result.AddRange(current.EnumerateFiles(searchPattern, options).Where(file => !hasFilter || predicate!(file)));

                foreach (DirectoryInfo subDir in current.EnumerateDirectories("*", options ?? DefaultOptions))
                    stack.Push(subDir);
            }
            catch
            {
                current.AddToErrorPaths();
            }
        }

        return result;
    }

    public static void AddToErrorPaths(this DirectoryInfo folder) => ErrorPaths.Add(folder.FullName + Path.DirectorySeparatorChar);

    public static bool IsErrorPath(this DirectoryInfo folder)
    {
        string fullPath = folder.FullName + Path.DirectorySeparatorChar;

        return ErrorPaths.Contains(fullPath) || ErrorPaths.Any(folder.FullName.StartsWith);
    }

    public static void ClearErrorPaths() => ErrorPaths.Clear();

    public static async Task<List<DirectoryInfo>> SafeGetLeafDirectoriesAsync(this DirectoryInfo root,
                                                                              DirectoryFilterDelegate predicate = null,
                                                                              string searchPattern = "*",
                                                                              EnumerationOptions options = null) =>
        await root.SafeGetAllDirectoriesAsync(dir => dir.IsLeaf() && (predicate is null || predicate(dir)), searchPattern, options);

    /// <summary>
    /// Given a list of folders, returns only those which are not subfolders
    /// of another folder in the list ("top-level" relative to each other).
    /// </summary>
    public static List<DirectoryInfo> GetTopLevelFolders(this IList<DirectoryInfo> folders, bool logFindings = false)
    {
        List<DirectoryInfo> topLevelFolders = [];

        foreach (DirectoryInfo folder in folders.OrderBy(dir => dir.FullName, FExFoundation.AlphanumComparatorFast))
        {
            if (!folders.Any(parent => parent != folder && folder.FullName.StartsWith(parent.FullName + Path.DirectorySeparatorChar)))
            {
                topLevelFolders.Add(folder);

                if (logFindings)
                    Log.Debug($"Found {folder.FullName}");
            }
        }

        return topLevelFolders;
    }

    public static bool IsEmpty(this DirectoryInfo directory, Func<IReadOnlyCollection<FileSystemInfo>, bool> predicate = null) =>
        RunSecure(directory,
            () =>
            {
                IEnumerable<FileSystemInfo> contentsEnumerable = directory.EnumerateFileSystemInfos("*", DefaultOptions);

                if (predicate is null)
                    return !contentsEnumerable.Any();

                FileSystemInfo[] contents = contentsEnumerable.ToArray();

                return contents.Length == 0 || predicate(contents);
            });

    public static bool IsLeaf(this DirectoryInfo directory) => RunSecure(directory, () => !directory.EnumerateDirectories("*", DefaultOptions).Any());

    public static bool SafeDelete(this DirectoryInfo folder, bool recursive = false, bool logDeletions = false)
    {
        try
        {
            folder.Refresh();

            if (folder.Exists)
            {
                folder.Delete(recursive);

                if (logDeletions)
                    Log.Debug($"Deleted: {folder.FullName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            if (logDeletions)
                Log.Error(ex, $"Failed to delete: {folder.FullName}");

            return false;
        }
    }

    public static bool SafeDelete(this FileInfo file, bool logDeletions = false)
    {
        try
        {
            file.Refresh();

            if (file.Exists)
            {
                file.Delete();

                if (logDeletions)
                    Log.Debug($"Deleted: {file.FullName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to delete: {file.FullName}");

            return false;
        }
    }

    private static async Task<List<DirectoryInfo>> GetDirectoriesAsync(DirectoryInfo current,
                                                                       bool hasFilter,
                                                                       DirectoryFilterDelegate predicate,
                                                                       string searchPattern,
                                                                       EnumerationOptions options)
    {
        if (current.IsErrorPath())
            return [];

        try
        {
            DirectoryInfo[] directories = await Queue.EnqueueAsync(() => Task.Run(() => current.GetDirectories(searchPattern, options)));

            List<DirectoryInfo>[] results = await directories.RunWithWhenAllTasksAsync(dir =>
                GetDirectoriesAsync(dir, hasFilter, predicate, searchPattern, options));

            return results.SelectMany(x => x).Concat(directories.Where(subDir => !hasFilter || predicate(subDir))).ToList();
        }
        catch
        {
            current.AddToErrorPaths();

            return [];
        }
    }

    private static T RunSecure<T>(DirectoryInfo directory, Func<T> func, T fallback = default)
    {
        if (directory.IsErrorPath())
            return fallback;

        try
        {
            return func();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"{ex.GetType().Name} for: {directory.FullName}");
            directory.AddToErrorPaths();

            return fallback;
        }
    }
}