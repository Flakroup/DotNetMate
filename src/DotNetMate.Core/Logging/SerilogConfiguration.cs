using Serilog;
using Serilog.Events;
using System;

namespace DotNetMate.Core.Logging;

public class SerilogConfiguration
{
    public static void ConfigureLogging()
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Async(wt =>
                wt.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

        config.WriteTo.Sentry(o =>
        {
            o.Dsn = "https://f9eff01861bdfdf395fa2a24a37a7c55@o4506098967052288.ingest.us.sentry.io/4511021782204421";
            o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
            o.MinimumEventLevel = LogEventLevel.Error;
            o.AttachStacktrace = true;
            o.AutoSessionTracking = true;
        });

        Log.Logger = config.CreateLogger();
    }
}