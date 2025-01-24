using Serilog;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMateTool;

public class Program
{
    public static async Task Main(string[] args)
    {
        var rootFolderOption = new Option<DirectoryInfo>("--folder",
            result => !result.Tokens.Any()
                ? new(Directory.GetCurrentDirectory())
                : new(result.Tokens.Single().Value),
            true,
            "The root folder of recursive scan.");

        var rootCommand = new RootCommand("DotNetMate");

        var cleanCommand = new Command("clean", "Remove temporary directories like bin, obj, .vs...")
        {
            rootFolderOption
        };

        rootCommand.AddCommand(cleanCommand);

        cleanCommand.SetHandler(DirectoryCleaner.CleanAsync, rootFolderOption);

        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.Async(wt =>
                wt.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .CreateLogger();

        await rootCommand.InvokeAsync(args);
    }
}