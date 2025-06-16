using DotNetMate.Core.Logging;
using FEx.DependencyInjection;
using FEx.DI.Abstractions;
using FEx.Logging;
using Serilog;
using StrongInject;
using System;
using System.Threading.Tasks;

namespace DotNetMateTool;

public class Program
{
    public static async Task Main()
    {
        var isLoggingConfigured = false;

        try
        {
            using DotNetMateContainer container = FExServiceProvider.Initialize<DotNetMateContainer, FExStrongInjectServiceProvider>();
            isLoggingConfigured = true;
            SerilogConfiguration.ConfigureLogging();
            using Owned<DotNetMateRunner> owned = container.Resolve<DotNetMateRunner>();
            DotNetMateRunner runner = owned.Value;
            await runner.InvokeAsync();
        }
        catch (Exception ex)
        {
            if (isLoggingConfigured)
                Log.Error(ex, ex.ToString());
            else
                GlobalLogger.LogError(ex.Message, ex);
        }
        finally
        {
            if (isLoggingConfigured)
                await Log.CloseAndFlushAsync();
        }
    }
}