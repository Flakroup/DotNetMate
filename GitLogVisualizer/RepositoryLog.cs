using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GitLogVisualizer;

public class RepositoryLog
{
    private static readonly string[] GenericBranches = ["main", "master", "develop", "development"];

    private readonly RepositoryInfo _repositoryInfo;

    public Signature Me => _repositoryInfo.Me;
    public string Name => _repositoryInfo.Name;
    public IReadOnlyCollection<CommitInfo> Log { get; }

    public RepositoryLog(RepositoryInfo repositoryInfo)
    {
        _repositoryInfo = repositoryInfo;

        Log = _repositoryInfo.AllBranchesInfo.Values
            .SelectMany(x => x.MyCommits.Select(c => new CommitInfo(_repositoryInfo, x.Branch, c)))
            .Concat(_repositoryInfo.GetOrphanedMergedCommits()) // commits from deleted branches
            .OrderBy(x => IsGenericBranch(x.Branch)) // feature branches first, so DistinctBy keeps them
            .DistinctBy(x => x.Commit.Sha)
            .OrderByDescending(x => x.When)
            .ToList()
            .AsReadOnly();
    }

    private static bool IsGenericBranch(Branch branch)
    {
        var name = branch.FriendlyName.Split('/').Last();

        return GenericBranches.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}