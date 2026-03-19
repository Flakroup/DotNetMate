using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DotNetMate.Core.Configuration;

public static class MateConfigLoader
{
    private const string ConfigFileName = ".mate.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static MateConfig Load(string workingDirectory = null)
    {
        workingDirectory ??= Directory.GetCurrentDirectory();

        var configFiles = FindConfigFiles(workingDirectory);
        var globalConfig = FindGlobalConfig();

        if (globalConfig is not null)
            configFiles.Add(globalConfig);

        if (configFiles.Count == 0)
        {
            Log.Debug("No .mate.json configuration files found");

            return new MateConfig();
        }

        // Merge from furthest (global) to closest (CWD) - closest wins
        configFiles.Reverse();
        var result = new MateConfig();

        foreach (var configFile in configFiles)
        {
            var loaded = LoadFile(configFile);

            if (loaded is not null)
                Merge(result, loaded);
        }

        return result;
    }

    private static List<string> FindConfigFiles(string startDirectory)
    {
        var files = new List<string>();
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            var configPath = Path.Combine(current.FullName, ConfigFileName);

            if (File.Exists(configPath))
            {
                Log.Debug("Found config: {ConfigPath}", configPath);
                files.Add(configPath);
            }

            current = current.Parent;
        }

        return files;
    }

    private static string FindGlobalConfig()
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var globalPath = Path.Combine(homePath, ConfigFileName);

        return File.Exists(globalPath) ? globalPath : null;
    }

    private static MateConfig LoadFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<MateConfig>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load config: {ConfigPath}", path);

            return null;
        }
    }

    /// <summary>
    /// Merges override into target. Non-null sections in override replace entire sections in target
    /// (appsettings-style: section-level override, not property-level).
    /// </summary>
    private static void Merge(MateConfig target, MateConfig source)
    {
        if (source.Clean is not null)
            target.Clean = source.Clean;

        if (source.GitLog is not null)
            target.GitLog = source.GitLog;

        if (source.ReSharper is not null)
            target.ReSharper = source.ReSharper;
    }
}
