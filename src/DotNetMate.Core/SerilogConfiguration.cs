using Serilog;

namespace DotNetMate.Core;

public class SerilogConfiguration
{
    public static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
            .WriteTo.Async(wt =>
                wt.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .CreateLogger();
    }
}