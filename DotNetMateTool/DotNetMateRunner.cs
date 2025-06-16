using DotNetMate.Core.IO;
using DotNetMate.Core.JB;
using FEx.Abstractions.Flow;
using GitLogVisualizer;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Error = FEx.Abstractions.Flow.Errors.Error;

namespace DotNetMateTool;

public class DotNetMateRunner
{
    private readonly string[] _args;
    private readonly RootCommand _rootCommand;

    public DotNetMateRunner()
    {
        _args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        _rootCommand = new("DotNetMate");
        _rootCommand.AddCommand(GetCleanCommand());
        _rootCommand.AddCommand(GetGitLogCommand());
        _rootCommand.AddCommand(GetReSharperCommand());
        _rootCommand.AddCommand(GetRemoveEmptyFoldersCommand());
        //_rootCommand.TreatUnmatchedTokensAsErrors = false;
    }

    public async Task InvokeAsync() => await _rootCommand.InvokeAsync(_args);

    private static Command GetReSharperCommand()
    {
        var resharperCommand = new Command("resharper", "Act on ReSharper");
        resharperCommand.AddCommand(GetReSharperCleanCommand());
        resharperCommand.AddCommand(GetReSharperSettingsCommand());

        return resharperCommand;
    }

    private static Command GetReSharperSettingsCommand()
    {
        Option<FileInfo> sortOption = new Option<FileInfo>(["-s", "--sort"],
            result => result.Tokens.Any()
                ? new FileInfo(result.Tokens.Single().Value)
                : null,
            description: "Sorts contents of DotSettings file").ExistingOnly();

        sortOption.AddValidator(result => Validate<FileInfo>(result, ReSharperService.ValidateDotSettingsFile));

        var settingsCommand = new Command("config", "Acts on DotSettings")
        {
            sortOption
        };

        settingsCommand.SetHandler(ReSharperService.OrderConfigAsync, sortOption);

        return settingsCommand;
    }

    private static Command GetReSharperCleanCommand()
    {
        var cleanCommand = new Command("clean", "Cleans temporary directories");
        cleanCommand.SetHandler(ReSharperService.CleanCachesAsync);

        return cleanCommand;
    }

    private static void Validate<T>(OptionResult result, Func<T, Result<Error>> validator)
    {
        T argument = result.GetValueOrDefault<T>();
        Result<Error> validationResult = validator(argument);

        if (validationResult.IsSuccess)
            return;

        result.ErrorMessage = validationResult.Error.Message;
    }

    private static DirectoryInfo GetFolderArg(ArgumentResult result, string path) =>
        new(result.Tokens.Any()
            ? result.Tokens.Single().Value
            : path ?? Directory.GetCurrentDirectory());

    private Command GetGitLogCommand()
    {
        var rootFolderOption = new Option<DirectoryInfo>(["-r", "--root"],
            result => GetFolderArg(result, _args.ElementAtOrDefault(1)),
            true,
            "The root of Git repositories recursive search.");

        var startDateOption = new Option<DateTime>(["-f", "--from"], "Oldest commit expected date.")
        {
            IsRequired = true
        };

        var excludedOption = new Option<IEnumerable<string>>(["-e", "--exclude"],
            result => result.Tokens.Any()
                ? result.Tokens.Single().Value.Split(',')
                : null,
            description: "Exclude repositories of name.");

        var exportToJsonOption = new Option<bool>(["-j", "--json"], "Save to json file.");

        var pathToJsonOption = new Option<FileInfo>(["-w", "--with"], "Merge with json file.");

        var exportToCsvOption = new Option<bool>(["-c", "--csv"], "Export as CSV.");

        var gitLogCommand = new Command("gitlog", "Prints log of commits done by user across many repositories")
        {
            rootFolderOption,
            startDateOption,
            excludedOption,
            exportToJsonOption,
            pathToJsonOption,
            exportToCsvOption
        };

        gitLogCommand.SetHandler(GitLogService.PrintGitLogAsync,
            rootFolderOption,
            startDateOption,
            excludedOption,
            exportToJsonOption,
            pathToJsonOption,
            exportToCsvOption);

        return gitLogCommand;
    }

    private Command GetCleanCommand()
    {
        Option<DirectoryInfo> rootFolderOption = new Option<DirectoryInfo>(["-f", "--folder"],
            result => GetFolderArg(result, _args.ElementAtOrDefault(1)),
            true,
            "The root folder of recursive scan.").ExistingOnly();

        var cleanCommand = new Command("clean", "Remove temporary directories like bin, obj, .vs...")
        {
            rootFolderOption
        };

        cleanCommand.SetHandler(DirectoryCleaner.CleanAsync, rootFolderOption);

        return cleanCommand;
    }

    private Command GetRemoveEmptyFoldersCommand()
    {
        var rootFolderOption = new Option<DirectoryInfo>("--folder",
            result => GetFolderArg(result, _args.ElementAtOrDefault(1)),
            true,
            "The root folder of recursive scan.");

        var cleanCommand = new Command("removeEmpty", "Remove empty directories")
        {
            rootFolderOption
        };

        cleanCommand.SetHandler(dir => DirectoryCleaner.RemoveEmptyDirectoriesAsync(dir, true), rootFolderOption);

        return cleanCommand;
    }
}