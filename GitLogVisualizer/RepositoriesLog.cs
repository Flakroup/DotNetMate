using System;
using System.Linq;

namespace GitLogVisualizer;

public class RepositoriesLog
{
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
        BranchName = commitInfo.Branch.FriendlyName.Split('/').Last();
        When = commitInfo.When;
        CommitId = commitInfo.Commit.Id.ToString(7);
        CommitMessage = commitInfo.Commit.MessageShort;
    }
}