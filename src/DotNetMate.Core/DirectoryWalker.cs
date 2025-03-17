using FEx.Abstractions;
using FEx.Basics.Collections.Concurrent;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotNetMate.Core;

public static class DirectoryWalker
{
    private static ConcurrentHashSet<string> ErrorPaths { get; }
    private static EnumerationOptions DefaultOptions { get; }

    static DirectoryWalker()
    {
        ErrorPaths = [];

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
    public static List<DirectoryInfo> SafeGetAllDirectories(string rootPath,
                                                            DirectoryFilterDelegate predicate = null,
                                                            string searchPattern = "*",
                                                            EnumerationOptions options = null) =>
        SafeGetAllDirectories(new DirectoryInfo(rootPath), predicate, searchPattern, options);

    /// <summary>
    /// Recursively gets all subdirectories from a root directory, ignoring
    /// any directories that throw an UnauthorizedAccessException or other IO exceptions.
    /// </summary>
    public static List<DirectoryInfo> SafeGetAllDirectories(this DirectoryInfo root,
                                                            DirectoryFilterDelegate predicate = null,
                                                            string searchPattern = "*",
                                                            EnumerationOptions options = null)
    {
        if (!root.Exists)
            throw new DirectoryNotFoundException($"Specified path doesn't exist: {root.FullName}");

        bool hasFilter = predicate is not null;
        options ??= DefaultOptions;

        var result = new List<DirectoryInfo>();
        var stack = new Stack<DirectoryInfo>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            DirectoryInfo current = stack.Pop();

            if (current.IsErrorPath())
                continue;

            try
            {
                foreach (DirectoryInfo subDir in current.EnumerateDirectories(searchPattern, options))
                {
                    if (!hasFilter
                        || predicate(subDir))
                        result.Add(subDir);

                    stack.Push(subDir);
                }
            }
            catch
            {
                AddToErrorPaths(current);
            }
        }

        return result;
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
                result.AddRange(current.EnumerateFiles(searchPattern, options)
                    .Where(file => !hasFilter || predicate!(file)));

                foreach (DirectoryInfo subDir in current.EnumerateDirectories("*", options ?? DefaultOptions))
                    stack.Push(subDir);
            }
            catch
            {
                AddToErrorPaths(current);
            }
        }

        return result;
    }

    public static void AddToErrorPaths(this DirectoryInfo folder) =>
        ErrorPaths.Add(folder.FullName + Path.DirectorySeparatorChar);

    public static bool IsErrorPath(this DirectoryInfo folder)
    {
        string fullPath = folder.FullName + Path.DirectorySeparatorChar;

        return ErrorPaths.Contains(fullPath) || ErrorPaths.Any(folder.FullName.StartsWith);
    }

    public static void ClearErrorPaths() => ErrorPaths.Clear();

    public static List<DirectoryInfo> SafeGetLeafDirectories(this DirectoryInfo root,
                                                             DirectoryFilterDelegate predicate = null,
                                                             string searchPattern = "*",
                                                             EnumerationOptions options = null) =>
        SafeGetAllDirectories(root,
            dir => dir.IsLeaf() && (predicate is null || predicate(dir)),
            searchPattern,
            options);

    /// <summary>
    /// Given a list of folders, returns only those which are not subfolders
    /// of another folder in the list ("top-level" relative to each other).
    /// </summary>
    public static List<DirectoryInfo> GetTopLevelFolders(this IList<DirectoryInfo> folders, bool logFindings = false)
    {
        List<DirectoryInfo> topLevelFolders = [];

        foreach (DirectoryInfo folder in folders.OrderBy(dir => dir.FullName, FExFoundation.AlphanumComparatorFast))
        {
            if (!folders.Any(parent =>
                    parent != folder && folder.FullName.StartsWith(parent.FullName + Path.DirectorySeparatorChar)))
            {
                topLevelFolders.Add(folder);

                if (logFindings)
                    Log.Debug($"Found {folder.FullName}");
            }
        }

        return topLevelFolders;
    }

    public static bool IsEmpty(this DirectoryInfo directory) =>
        RunSecure(directory, () => !directory.EnumerateFileSystemInfos("*", DefaultOptions).Any());

    public static bool IsLeaf(this DirectoryInfo directory) =>
        RunSecure(directory, () => !directory.EnumerateDirectories("*", DefaultOptions).Any());

    private static T RunSecure<T>(DirectoryInfo directory, Func<T> func, T fallback = default)
    {
        if (directory.IsErrorPath())
            return fallback;

        try
        {
            return func();
        }
        catch
        {
            directory.AddToErrorPaths();

            return fallback;
        }
    }

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
            if (logDeletions)
                Log.Error(ex, $"Failed to delete: {file.FullName}");

            return false;
        }
    }
}