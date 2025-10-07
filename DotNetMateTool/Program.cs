using DotNetMate.Core.Logging;
using FEx.Agnostics.Abstractions.Logging;
using FEx.DependencyInjection;
using FEx.DependencyInjection.Abstractions;
using Serilog;
using StrongInject;
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
            using DotNetMateContainer container =
                FExServiceProvider.Initialize<DotNetMateContainer, FExStrongInjectServiceProvider>();

            isLoggingConfigured = true;
            SerilogConfiguration.ConfigureLogging();
            using Owned<DotNetMateRunner> owned = container.Resolve<DotNetMateRunner>();
            DotNetMateRunner runner = owned.Value;
            int exitCode = await runner.InvokeAsync();

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
            if (isLoggingConfigured)
                await Log.CloseAndFlushAsync();
        }
    }
}