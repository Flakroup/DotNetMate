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

        var sentryDsn = Environment.GetEnvironmentVariable("SENTRY_DSN");

        if (!string.IsNullOrEmpty(sentryDsn))
        {
            config.WriteTo.Sentry(o =>
            {
                o.Dsn = sentryDsn;
                o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
                o.MinimumEventLevel = LogEventLevel.Error;
                o.AttachStacktrace = true;
                o.AutoSessionTracking = true;
            });
        }

        Log.Logger = config.CreateLogger();
    }
}