using FEx.Common.Extensions;
using FEx.Extensions;
using FEx.Json.Extensions;
using FEx.WakaTime;
using FEx.WakaTime.Models;
using Humanizer;
using LibGit2Sharp;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WakaTime;

namespace GitLogVisualizer;

public class GitLogService
{
    private static readonly SemaphoreSlim LogSemaphore = new(1, 1);
    private static double _max;
    private static double _prg;

    public static async Task PrintGitLogAsync(DirectoryInfo root,
                                              DateTime startDate,
                                              IEnumerable<string> excluded,
                                              bool exportToJson,
                                              FileInfo jsonToMerge,
                                              bool exportToCsv)
    {
        if (!root.Exists)
        {
            Log.Warning($"Directory {root.FullName} doesn't exist");

            return;
        }

        Log.Information("Listing available repositories");

        var directories = root.EnumerateDirectories()
            .Where(x => (excluded is null || !excluded.Contains(x.Name)) && Repository.IsValid(x.FullName))
            .ToList();

        _max = directories.Count;

        if (_max == 0)
        {
            Log.Warning($"There are no repositories here: {root.FullName}");

            return;
        }

        Log.Information("Getting info about repositories");

        RepositoryInfo[] repos = await directories.RunWithWhenAllAsync(directoryInfo =>
            GetRepositoryInformation(directoryInfo, startDate));

        Log.Information(string.Empty);

        List<RepositoriesLog> mergedJsonLog = await GetMergedJsonLogAsync(jsonToMerge);

        List<RepositoryLog> myLogs = GetMyLogs(repos);

        List<RepositoriesLog> allLogs = GetAllLogs(myLogs, mergedJsonLog);

        if (exportToJson)
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "log.json"),
                allLogs.ToJson(formatting: Formatting.Indented));

        DateTime now = DateTime.Now;
        Log.Information($"Listing {myLogs.First().Me.Name} commits after {startDate:f}");
        var sb = new StringBuilder();
        DateTime date = DateTime.MinValue;

        foreach (RepositoriesLog log in allLogs)
        {
            if (date != log.When.DateTime.Date)
            {
                date = log.When.DateTime.Date;
                Log.Information(date.ToString("d"));
            }

            Log.Information(
                $"\t{log.RepositoryName}\t{log.BranchName}\t{log.When.DateTime.Humanize(false, now)}\t{log.When.DateTime:f}\t{log.CommitId} {log.CommitMessage}");

            Log.Information(string.Empty);

            if (exportToCsv)
                sb.Append(
                    $"{log.RepositoryName};{log.BranchName};{log.When.DateTime.Humanize(false, now)};{log.When.DateTime:f};{log.CommitId};{log.CommitMessage}\n");
        }

        if (exportToCsv)
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "log.csv"), sb.ToString(), Encoding.UTF8);
    }

    //private static async Task AfterCallAsync(FlurlCall call)
    //{
    //    string body = await call.Response.GetStringAsync();
    //    var sb = new StringBuilder(
    //        $"Finished {call.Request.Verb} {call.Request.Url} responded with {call.Response.StatusCode}");
    //    if (body is not null)
    //        sb.AppendLine().AppendLine(body);
    //    Log.Info(sb.ToString());
    //}

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    protected static async Task<bool> GetWakatimeInfoAsync()
    {
        WakaTimeConfigFile.Read();

        if (WakaTimeConfigFile.ApiKey.IsNullOrEmptyString())
            return false;

        var client = new WakaTimeClient(WakaTimeConfigFile.ApiKey);
        StatsResponse o = await client.GetUserStatsAsync("last_7_days");
        UserResponse u = await client.GetCurrentUserAsync();
        DurationResponse d = await client.GetDurationsAsync("2023-09-30");

        return o is not null && u is not null && d is not null;
    }

    private static List<RepositoriesLog> GetAllLogs(List<RepositoryLog> myLogs, List<RepositoriesLog> mergedJsonLog)
    {
        var allLogs = myLogs.SelectMany(x => x.Log)
            .Select(x => new RepositoriesLog(x))
            .Concat(mergedJsonLog)
            .DistinctBy(x => x.CommitId)
            .OrderBy(x => x.When)
            .ToList();

        return allLogs;
    }

    private static List<RepositoryLog> GetMyLogs(RepositoryInfo[] repos)
    {
        var reposWithBranchesInfo = repos.Where(HasAnyBranchesInfo).ToList();

        var myBranchInfos = reposWithBranchesInfo.SelectMany(r => r.AllBranchesInfo.Values)
            .Where(HasAnyMyCommits)
            .ToList();

        var myLogs = myBranchInfos.Select(x => new RepositoryLog(x.RepositoryInfo))
            .Where(logInfo => logInfo.Log.Count > 0)
            .ToList();

        return myLogs;
    }

    private static async Task<List<RepositoriesLog>> GetMergedJsonLogAsync(FileInfo jsonToMerge)
    {
        List<RepositoriesLog> mergedJsonLog = [];

        if (jsonToMerge is not null
            && jsonToMerge.Exists)
        {
            await using FileStream fileStream = File.Open(jsonToMerge.FullName, FileMode.Open);
            mergedJsonLog = fileStream.DeserializeFromStream<List<RepositoriesLog>>();
        }

        return mergedJsonLog;
    }

    private static bool HasAnyMyCommits(BranchInfo x) => x.MyCommits.Any();

    private static bool HasAnyBranchesInfo(RepositoryInfo r) => r.AllBranchesInfo.Any();

    private static RepositoryInfo GetRepositoryInformation(DirectoryInfo directoryInfo, DateTime? loadCommitsAfter)
    {
        var res = RepositoryInfo.GetRepositoryInformationForPath(directoryInfo, loadCommitsAfter);
        _ = Task.Run(() => LogProgressAsync(directoryInfo.Name));

        return res;
    }

    private static async Task LogProgressAsync(string message)
    {
        await LogSemaphore.WaitAsync();

        try
        {
            _prg++;
            Log.Information($"{Math.Floor(_prg / _max * 100D)}% {message}");
        }
        finally
        {
            LogSemaphore.Release();
        }
    }
}