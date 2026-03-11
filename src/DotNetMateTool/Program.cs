using DotNetMate.Core.Logging;
using FEx.Agnostics.Abstractions.Logging;
using FEx.DependencyInjection.Abstractions;
using Sentry;
using Serilog;
using System;
using System.Threading.Tasks;

namespace DotNetMateTool;

public class Program
{
    public static async Task<int> Main()
    {
        Banner.Display();

        var isLoggingConfigured = false;

        try
        {
            using var container = await FExServiceProvider.InitializeAsync<DotNetMateContainer>();

            isLoggingConfigured = true;
            SerilogConfiguration.ConfigureLogging();
            using var owned = container.Resolve<DotNetMateRunner>();
            var runner = owned.Value;
            var exitCode = await runner.InvokeAsync();

            return exitCode;
        }
        catch (Exception ex)
        {
            if (isLoggingConfigured)
                Log.Error(ex, ex.ToString());
            else
                FExStaticLogger.Error(ex);

            return 1;
        }
        finally
        {
            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));

            if (isLoggingConfigured)
                await Log.CloseAndFlushAsync();
        }
    }
}