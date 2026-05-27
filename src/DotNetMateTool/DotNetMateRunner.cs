using DotNetMate.Core.Configuration;
using DotNetMate.Core.IO;
using DotNetMate.Core.JB;
using GitLogVisualizer;
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMateTool;

public class DotNetMateRunner
{
    private readonly string[] _args;
    private readonly RootCommand _rootCommand;
    private readonly MateConfig _config;

    public DotNetMateRunner()
    {
        _args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        _config = MateConfigLoader.Load();
        _rootCommand = new("DotNetMate");
        _rootCommand.Subcommands.Add(GetCleanCommand());
        _rootCommand.Subcommands.Add(GetGitLogCommand());
        _rootCommand.Subcommands.Add(GetReSharperCommand());
        _rootCommand.Subcommands.Add(GetRemoveEmptyFoldersCommand());
        _rootCommand.Subcommands.Add(GetCompletionsCommand());
    }

    public async Task<int> InvokeAsync()
    {
        var parseResult = _rootCommand.Parse(_args);

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
            var file = result.GetValueOrDefault<FileInfo>();

            if (file is { Exists: false })
            {
                result.AddError($"File does not exist: {file.FullName}");

                return;
            }

            var validationResult = ReSharperService.ValidateDotSettingsFile(file);

            if (!validationResult.IsSuccess)
                result.AddError(validationResult.Error.Message);
        });

        var settingsCommand = new Command("config", "Acts on DotSettings");
        settingsCommand.Options.Add(sortOption);

        settingsCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var sortFile = parseResult.GetValue(sortOption);
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
        var gitLogConfig = _config.GitLog;

        var rootFolderOption = new Option<DirectoryInfo>("--root")
        {
            Description = "The root of Git repositories recursive search.",
            DefaultValueFactory = _ => new(_args.ElementAtOrDefault(1) ?? Directory.GetCurrentDirectory())
        };

        rootFolderOption.Aliases.Add("-r");

        DateTime defaultDate = default;
        var hasDefaultAfterValue = !string.IsNullOrEmpty(gitLogConfig?.DefaultAfter);
        var hasValidDefault = hasDefaultAfterValue && DateTime.TryParse(gitLogConfig.DefaultAfter, out defaultDate);

        if (hasDefaultAfterValue && !hasValidDefault)
            Serilog.Log.Warning("Invalid date format in .mate.json gitLog.defaultAfter: {Value}", gitLogConfig.DefaultAfter);

        var startDateOption = new Option<DateTime>("--from")
        {
            Description = "Oldest commit expected date."
        };

        startDateOption.Aliases.Add("-f");
        startDateOption.Required = !hasValidDefault;

        if (hasValidDefault)
            startDateOption.DefaultValueFactory = _ => defaultDate;

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

        var defaultTempo = gitLogConfig?.Tempo == true;

        var tempoOption = new Option<bool>("--tempo")
        {
            Description = "Show time summary per branch per day for Tempo logging.",
            DefaultValueFactory = _ => defaultTempo
        };

        tempoOption.Aliases.Add("-t");

        var gitLogCommand = new Command("gitlog", "Prints log of commits done by user across many repositories");
        gitLogCommand.Options.Add(rootFolderOption);
        gitLogCommand.Options.Add(startDateOption);
        gitLogCommand.Options.Add(excludedOption);
        gitLogCommand.Options.Add(exportToJsonOption);
        gitLogCommand.Options.Add(pathToJsonOption);
        gitLogCommand.Options.Add(exportToCsvOption);
        gitLogCommand.Options.Add(tempoOption);

        gitLogCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var root = parseResult.GetValue(rootFolderOption);
            var startDate = parseResult.GetValue(startDateOption);
            var excludedStr = parseResult.GetValue(excludedOption);

            var excluded = !string.IsNullOrEmpty(excludedStr)
                ? excludedStr.Split(',').Select(static x => x.Trim()).Where(static x => x.Length > 0)
                : [];

            var exportJson = parseResult.GetValue(exportToJsonOption);
            var pathToJson = parseResult.GetValue(pathToJsonOption);
            var exportCsv = parseResult.GetValue(exportToCsvOption);
            var tempo = parseResult.GetValue(tempoOption);

            await GitLogService.PrintGitLogAsync(root,
                startDate,
                excluded,
                exportJson,
                pathToJson,
                exportCsv,
                tempo,
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
            var dir = result.GetValueOrDefault<DirectoryInfo>();

            if (dir is { Exists: false })
                result.AddError($"Directory does not exist: {dir.FullName}");
        });

        var includeWorktreesOption = new Option<bool>("--include-worktrees")
        {
            Description = "Clean bin/obj inside linked git worktrees (skipped by default)."
        };

        includeWorktreesOption.Aliases.Add("-w");

        var cleanCommand = new Command("clean", "Remove temporary directories like bin, obj, .vs...");
        cleanCommand.Options.Add(rootFolderOption);
        cleanCommand.Options.Add(includeWorktreesOption);

        var cleanConfig = _config.Clean;

        cleanCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var folder = parseResult.GetValue(rootFolderOption);
            var includeWorktrees = parseResult.GetValue(includeWorktreesOption);
            await DirectoryCleaner.CleanAsync(folder, cleanConfig, cancellationToken, includeWorktrees);

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

        rootFolderOption.Validators.Add(static result =>
        {
            var dir = result.GetValueOrDefault<DirectoryInfo>();

            if (dir is { Exists: false })
                result.AddError($"Directory does not exist: {dir.FullName}");
        });

        cleanCommand.Options.Add(rootFolderOption);

        cleanCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var dir = parseResult.GetValue(rootFolderOption);
            await DirectoryCleaner.RemoveEmptyDirectoriesAsync(dir, true, cancellationToken);

            return 0;
        });

        return cleanCommand;
    }

    private static Command GetCompletionsCommand()
    {
        var completionsCommand = new Command("completions", "Generate shell completion scripts");
        completionsCommand.Subcommands.Add(GetCompletionSubcommand("powershell", PowerShellCompletionScript));
        completionsCommand.Subcommands.Add(GetCompletionSubcommand("bash", BashCompletionScript));
        completionsCommand.Subcommands.Add(GetCompletionSubcommand("zsh", ZshCompletionScript));

        return completionsCommand;
    }

    private static Command GetCompletionSubcommand(string shell, string script)
    {
        var command = new Command(shell, $"Generate {shell} completion script");

        command.SetAction((_, _) =>
        {
            Console.WriteLine(script);

            return Task.FromResult(0);
        });

        return command;
    }

    private const string PowerShellCompletionScript = """
        Register-ArgumentCompleter -Native -CommandName mate -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)
            $command = $commandAst.ToString()
            dotnet-suggest get --executable mate --position $cursorPosition -- "$command" | ForEach-Object {
                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
            }
        }
        """;

    private const string BashCompletionScript = """
        _mate_completions() {
            local completions
            completions=$(dotnet-suggest get --executable mate --position ${COMP_POINT} -- "${COMP_LINE}" 2>/dev/null)
            COMPREPLY=( $(compgen -W "$completions" -- "${COMP_WORDS[COMP_CWORD]}") )
        }
        complete -F _mate_completions mate
        """;

    private const string ZshCompletionScript = """
        _mate_completions() {
            local completions
            completions=$(dotnet-suggest get --executable mate --position ${CURSOR} -- "${BUFFER}" 2>/dev/null)
            reply=(${(f)completions})
        }
        compctl -K _mate_completions mate
        """;
}
