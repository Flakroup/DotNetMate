using DotNetMate.Core.IO;
using DotNetMate.Core.JB;
using FEx.Agnostics.Abstractions.Flow;
using GitLogVisualizer;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMateTool;

public class DotNetMateRunner
{
    private readonly string[] _args;
    private readonly RootCommand _rootCommand;

    public DotNetMateRunner()
    {
        _args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        _rootCommand = new("DotNetMate");
        _rootCommand.Subcommands.Add(GetCleanCommand());
        _rootCommand.Subcommands.Add(GetGitLogCommand());
        _rootCommand.Subcommands.Add(GetReSharperCommand());
        _rootCommand.Subcommands.Add(GetRemoveEmptyFoldersCommand());
    }

    public async Task<int> InvokeAsync()
    {
        ParseResult parseResult = _rootCommand.Parse(_args);

        return await parseResult.InvokeAsync();
    }

    private static Command GetReSharperCommand()
    {
        var resharperCommand = new Command("resharper", "Act on ReSharper");
        resharperCommand.Subcommands.Add(GetReSharperCleanCommand());
        resharperCommand.Subcommands.Add(GetReSharperSettingsCommand());

        return resharperCommand;
    }

    private static Command GetReSharperSettingsCommand()
    {
        var sortOption = new Option<FileInfo>("--sort")
        {
            Description = "Sorts contents of DotSettings file"
        };

        sortOption.Aliases.Add("-s");

        sortOption.Validators.Add(result =>
        {
            FileInfo file = result.GetValueOrDefault<FileInfo>();

            if (file is { Exists: false })
            {
                result.AddError($"File does not exist: {file.FullName}");

                return;
            }

            Result<Error> validationResult = ReSharperService.ValidateDotSettingsFile(file);

            if (!validationResult.IsSuccess)
                result.AddError(validationResult.Error.Message);
        });

        var settingsCommand = new Command("config", "Acts on DotSettings");
        settingsCommand.Options.Add(sortOption);

        settingsCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            FileInfo sortFile = parseResult.GetValue(sortOption);
            await ReSharperService.OrderConfigAsync(sortFile, cancellationToken);

            return 0;
        });

        return settingsCommand;
    }

    private static Command GetReSharperCleanCommand()
    {
        var cleanCommand = new Command("clean", "Cleans temporary directories");

        cleanCommand.SetAction(static async (_, cancellationToken) =>
        {
            await ReSharperService.CleanCachesAsync(cancellationToken);

            return 0;
        });

        return cleanCommand;
    }

    private Command GetGitLogCommand()
    {
        var rootFolderOption = new Option<DirectoryInfo>("--root")
        {
            Description = "The root of Git repositories recursive search.",
            DefaultValueFactory = _ => new(_args.ElementAtOrDefault(1) ?? Directory.GetCurrentDirectory())
        };

        rootFolderOption.Aliases.Add("-r");

        var startDateOption = new Option<DateTime>("--from")
        {
            Description = "Oldest commit expected date."
        };

        startDateOption.Aliases.Add("-f");
        startDateOption.Required = true;

        var excludedOption = new Option<string>("--exclude")
        {
            Description = "Exclude repositories of name (comma-separated)."
        };

        excludedOption.Aliases.Add("-e");

        var exportToJsonOption = new Option<bool>("--json")
        {
            Description = "Save to json file."
        };

        exportToJsonOption.Aliases.Add("-j");

        var pathToJsonOption = new Option<FileInfo>("--with")
        {
            Description = "Merge with json file."
        };

        pathToJsonOption.Aliases.Add("-w");

        var exportToCsvOption = new Option<bool>("--csv")
        {
            Description = "Export as CSV."
        };

        exportToCsvOption.Aliases.Add("-c");

        var gitLogCommand = new Command("gitlog", "Prints log of commits done by user across many repositories");
        gitLogCommand.Options.Add(rootFolderOption);
        gitLogCommand.Options.Add(startDateOption);
        gitLogCommand.Options.Add(excludedOption);
        gitLogCommand.Options.Add(exportToJsonOption);
        gitLogCommand.Options.Add(pathToJsonOption);
        gitLogCommand.Options.Add(exportToCsvOption);

        gitLogCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            DirectoryInfo root = parseResult.GetValue(rootFolderOption);
            DateTime startDate = parseResult.GetValue(startDateOption);
            string excludedStr = parseResult.GetValue(excludedOption);

            IEnumerable<string> excluded = !string.IsNullOrEmpty(excludedStr)
                ? excludedStr.Split(',').AsEnumerable()
                : [];

            bool exportJson = parseResult.GetValue(exportToJsonOption);
            FileInfo pathToJson = parseResult.GetValue(pathToJsonOption);
            bool exportCsv = parseResult.GetValue(exportToCsvOption);

            await GitLogService.PrintGitLogAsync(root,
                startDate,
                excluded,
                exportJson,
                pathToJson,
                exportCsv,
                cancellationToken);

            return 0;
        });

        return gitLogCommand;
    }

    private Command GetCleanCommand()
    {
        var rootFolderOption = new Option<DirectoryInfo>("--folder")
        {
            Description = "The root folder of recursive scan.",
            DefaultValueFactory = _ => new(_args.ElementAtOrDefault(1) ?? Directory.GetCurrentDirectory())
        };

        rootFolderOption.Aliases.Add("-f");

        rootFolderOption.Validators.Add(static result =>
        {
            DirectoryInfo dir = result.GetValueOrDefault<DirectoryInfo>();

            if (dir is { Exists: false })
                result.AddError($"Directory does not exist: {dir.FullName}");
        });

        var cleanCommand = new Command("clean", "Remove temporary directories like bin, obj, .vs...");
        cleanCommand.Options.Add(rootFolderOption);

        cleanCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            DirectoryInfo folder = parseResult.GetValue(rootFolderOption);
            await DirectoryCleaner.CleanAsync(folder, cancellationToken);

            return 0;
        });

        return cleanCommand;
    }

    private Command GetRemoveEmptyFoldersCommand()
    {
        var cleanCommand = new Command("removeEmpty", "Remove empty directories");
        var rootFolderOption = new Option<DirectoryInfo>("--folder")
        {
            Description = "The root folder of recursive scan.",
            DefaultValueFactory = _ => new(_args.ElementAtOrDefault(1) ?? Directory.GetCurrentDirectory())
        };
        cleanCommand.Options.Add(rootFolderOption);

        cleanCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            DirectoryInfo dir = parseResult.GetValue(rootFolderOption);
            await DirectoryCleaner.RemoveEmptyDirectoriesAsync(dir, true, cancellationToken);

            return 0;
        });

        return cleanCommand;
    }
}