using FEx.Common.Extensions;
using System;
using System.Collections.Generic;
using System.IO;

namespace DotNetMateTool;

public class DirWalker
{
    /// <summary>
    /// Recursively gets all subdirectories from a root directory, ignoring
    /// any directories that throw an UnauthorizedAccessException or other IO exceptions.
    /// </summary>
    public static IEnumerable<DirectoryInfo> SafeGetAllDirectories(string rootPath)
    {
        var root = new DirectoryInfo(rootPath.Guard(nameof(rootPath)));

        if (!root.Exists)
            throw new ArgumentException($"Specified path doesn't exist: {rootPath}");

        return SafeGetAllDirectories(root);
    }

    /// <summary>
    /// Recursively gets all subdirectories from a root directory, ignoring
    /// any directories that throw an UnauthorizedAccessException or other IO exceptions.
    /// </summary>
    public static IEnumerable<DirectoryInfo> SafeGetAllDirectories(DirectoryInfo root)
    {
        if (!root.Exists)
            throw new ArgumentException($"Specified path doesn't exist: {root.FullName}");

        var result = new List<DirectoryInfo>();
        var stack = new Stack<DirectoryInfo>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            DirectoryInfo current = stack.Pop();
            DirectoryInfo[] subDirs;

            try
            {
                subDirs = current.GetDirectories();
            }
            catch
            {
                continue;
            }

            foreach (DirectoryInfo subDir in subDirs)
            {
                result.Add(subDir);
                stack.Push(subDir);
            }
        }

        return result;
    }
}