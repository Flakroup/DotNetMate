using GitLogVisualizer;
using Serilog;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMateTool;

public class Program
{
    public static async Task Main(string[] args)
    {
        var rootCommand = new RootCommand("DotNetMate");
        rootCommand.AddCommand(GetCleanCommand());
        rootCommand.AddCommand(GetGitLogCommand());
        rootCommand.AddCommand(GetReSharperCommand());

        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.Async(wt =>
                wt.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .CreateLogger();

        await rootCommand.InvokeAsync(args);
        await Log.CloseAndFlushAsync();
    }

    private static Command GetReSharperCommand()
    {
        var cleanOption = new Option<bool>(["-c", "--clean"],
            "Cleans temporary directories");

        var resharperCommand = new Command("resharper", "Act on ReSharper")
        {
            cleanOption
        };

        resharperCommand.SetHandler(ReSharperService.HandleAsync, cleanOption);

        return resharperCommand;
    }

    private static Command GetGitLogCommand()
    {
        var rootFolderOption = new Option<DirectoryInfo>(["-r", "--root"],
            result => !result.Tokens.Any()
                ? new(Directory.GetCurrentDirectory())
                : new(result.Tokens.Single().Value),
            true,
            "The root of Git repositories recursive search.")
        {
            IsRequired = true
        };

        var startDateOption = new Option<DateTime>(["-f", "--from"], "Oldest commit expected date.")
        {
            IsRequired = true
        };

        var excludedOption = new Option<IEnumerable<string>>(["-e", "--exclude"],
            result => result.Tokens.Any()
                ? result.Tokens.Single().Value.Split(',')
                : null,
            description: "Exclude repositories of name.");

        var exportToJsonOption = new Option<bool>(["-j", "--json"],
            description: "Save to json file.");

        var pathToJsonOption = new Option<FileInfo>(["-w", "--with"],
            description: "Merge with json file.");

        var exportToCsvOption = new Option<bool>(["-c", "--csv"],
            description: "Export as CSV.");

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

    private static Command GetCleanCommand()
    {
        var rootFolderOption = new Option<DirectoryInfo>("--folder",
            result => !result.Tokens.Any()
                ? new(Directory.GetCurrentDirectory())
                : new(result.Tokens.Single().Value),
            true,
            "The root folder of recursive scan.")
        {
            IsRequired = true
        };

        var cleanCommand = new Command("clean", "Remove temporary directories like bin, obj, .vs...")
        {
            rootFolderOption
        };

        cleanCommand.SetHandler(DirectoryCleaner.CleanAsync, rootFolderOption);

        return cleanCommand;
    }
}