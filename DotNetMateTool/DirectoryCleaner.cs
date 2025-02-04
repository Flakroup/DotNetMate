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
        var foldersToDelete = new List<DirectoryInfo>();

        if (!rootFolder.Exists)
            return foldersToDelete;

        IEnumerable<DirectoryInfo> allSubDirs = DirWalker.SafeGetAllDirectories(rootFolder);

        foldersToDelete.AddRange(allSubDirs.Select(dir => new
        {
            dir,
            name = dir.Name
        })
            .Where(arg => arg.name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                          || arg.name.Equals("obj", StringComparison.OrdinalIgnoreCase)
                          || arg.name.Equals(".vs", StringComparison.OrdinalIgnoreCase)
                          || arg.name.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
                          || arg.name.EndsWith("Installer-cache", StringComparison.OrdinalIgnoreCase))
            .Select(arg => arg.dir));

        return foldersToDelete;
    }

    /// <summary>
    /// Given a list of folders, returns only those which are not subfolders
    /// of another folder in the list ("top-level" relative to each other).
    /// In the PowerShell script, this ensures we don't double-delete parent
    /// and child.
    /// </summary>
    private static List<DirectoryInfo> GetTopLevelFolders(List<DirectoryInfo> folders)
    {
        var sorted = folders.OrderBy(f =>
                f.FullName.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Length)
            .ToList();

        var topLevel = new Dictionary<string, DirectoryInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (DirectoryInfo folder in sorted)
        {
            bool isSubfolder = topLevel.Keys.Any(existingKey =>
                folder.FullName.StartsWith(existingKey, StringComparison.OrdinalIgnoreCase));

            if (!isSubfolder)
            {
                Log.Debug($"Found {folder.FullName}");
                topLevel[folder.FullName] = folder;
            }
        }

        return topLevel.Values.OrderBy(f => f.FullName).ToList();
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