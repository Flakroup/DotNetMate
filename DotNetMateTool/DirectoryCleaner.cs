using FEx.Abstractions;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMateTool;

public class DirectoryCleaner
{
    public static async Task CleanAsync(DirectoryInfo targetFolder)
    {
        ArgumentNullException.ThrowIfNull(targetFolder);

        Log.Information($"Scanning\t{targetFolder}");

        List<DirectoryInfo> foldersToDelete = GetFoldersToDelete(targetFolder);
        Log.Information($"Found {foldersToDelete.Count} folders to delete.");

        Log.Information("Sorting folders");
        List<DirectoryInfo> topLevelFolders = GetTopLevelFolders(foldersToDelete);
        Log.Information($"Filtered {topLevelFolders.Count} top-level folders to delete.");

        Log.Information("Deleting...");

        await Task.WhenAll(topLevelFolders.Select(folder => Task.Run(() => RemoveFolder(folder))));

        Log.Information("Deletion process completed");

        List<DirectoryInfo> remainingFolders = GetFoldersToDelete(targetFolder);

        if (remainingFolders.Any())
        {
            Log.Warning("WARNING: There are some folders left that could not be deleted:");

            foreach (DirectoryInfo folder in remainingFolders)
                Log.Debug(folder.FullName);
        }
        else
        {
            Log.Information("All specified folders have been successfully deleted");
        }
    }

    /// <summary>
    /// Returns a list of all candidate folders to delete (bin, obj, .vs, .tmp,
    /// or any folder whose name ends with "Installer-cache").
    /// </summary>
    private static List<DirectoryInfo> GetFoldersToDelete(DirectoryInfo rootFolder)
    {
        if (!rootFolder.Exists)
            return [];

        return DirWalker.SafeGetAllDirectories(rootFolder,
            dir => dir.Name is "bin" or "obj" or ".vs" or ".tmp" or "TestResults"
                   || dir.Name.EndsWith("Installer-cache", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Given a list of folders, returns only those which are not subfolders
    /// of another folder in the list ("top-level" relative to each other).
    /// In the PowerShell script, this ensures we don't double-delete parent
    /// and child.
    /// </summary>
    private static List<DirectoryInfo> GetTopLevelFolders(List<DirectoryInfo> folders)
    {
        var topLevelFolders = folders
            .Where(folder => !folders.Any(parent =>
                parent != folder && folder.FullName.StartsWith(parent.FullName + Path.DirectorySeparatorChar)))
            .OrderBy(f => f.FullName, FExFoundation.AlphanumComparatorFast)
            .ToList();

        foreach (DirectoryInfo folder in topLevelFolders)
            Log.Debug($"Found {folder.FullName}");

        return topLevelFolders;
    }

    /// <summary>
    /// Attempts to delete a folder (recursively). Logs success or error.
    /// </summary>
    private static void RemoveFolder(DirectoryInfo folder)
    {
        try
        {
            folder.Refresh();

            if (folder.Exists)
            {
                folder.Delete(true); // recursive delete
                Log.Debug($"Deleted: {folder.FullName}");
            }
        }
        catch (Exception)
        {
            Log.Error($"Failed to delete: {folder.FullName}");
        }
    }
}