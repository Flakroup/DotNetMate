using System;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("DotNetMateTool.Tests")]

namespace DotNetMateTool;

internal static class UpdateChecker
{
    private const string PackageId = "DotNetMateTool";
    private const string NuGetUrl = $"https://api.nuget.org/v3-flatcontainer/{PackageId}/index.json";

    public static async Task<string> CheckAsync()
    {
        try
        {
            var current = GetCurrentVersion();

            if (current is null)
                return null;

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(5);

            var json = await http.GetStringAsync(NuGetUrl);
            var latest = ParseLatestVersion(json);

            return latest > current ? latest.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static Version GetCurrentVersion()
    {
        var info = typeof(UpdateChecker).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (info is null)
            return null;

        var clean = info.Contains('+') ? info[..info.IndexOf('+')] : info;

        return Version.TryParse(clean, out var v) ? v : null;
    }

    internal static Version ParseLatestVersion(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("versions", out var versions))
            return null;

        Version latest = null;

        using var enumerator = versions.EnumerateArray();

        while (enumerator.MoveNext())
        {
            var str = enumerator.Current.GetString();

            if (str is null || str.Contains('-'))
                continue;

            if (Version.TryParse(str, out var parsed) && (latest is null || parsed > latest))
                latest = parsed;
        }

        return latest;
    }
}
