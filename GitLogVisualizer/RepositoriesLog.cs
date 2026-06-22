using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace GitLogVisualizer;

public partial class RepositoriesLog
{
    private static readonly string[] GenericBranches = ["main", "master", "develop", "development"];

    public string CommitMessage { get; set; }

    public string CommitId { get; set; }

    public DateTimeOffset When { get; set; }

    public string BranchName { get; set; }

    public string RepositoryName { get; set; }

    public RepositoriesLog()
    {
    }

    public RepositoriesLog(CommitInfo commitInfo)
    {
        RepositoryName = commitInfo.Name;
        When = commitInfo.When;
        CommitId = commitInfo.Commit.Id.ToString(7);
        CommitMessage = commitInfo.Commit.MessageShort;

        if (commitInfo.BranchNameOverride != null)
        {
            BranchName = commitInfo.BranchNameOverride;
        }
        else
        {
            var branchName = commitInfo.Branch.FriendlyName.Split('/').Last();
            BranchName = IsGenericBranch(branchName)
                ? TryExtractSourceBranch(commitInfo.Commit.Message) ?? branchName
                : branchName;
        }
    }

    private static bool IsGenericBranch(string name) =>
        GenericBranches.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts source branch name from merge commit messages like:
    /// "Merge branch 'PM3-1305' into develop"
    /// "Merge branch 'feature/PM3-1305' into develop"
    /// </summary>
    private static string TryExtractSourceBranch(string commitMessage)
    {
        var match = MergeBranchPattern().Match(commitMessage);

        return match.Success
            ? match.Groups[1].Value.Split('/').Last()
            : null;
    }

    [GeneratedRegex("Merge branch '([^']+)'", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex MergeBranchPattern();
}