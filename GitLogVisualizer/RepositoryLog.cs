using LibGit2Sharp;
using System.Collections.Generic;
using System.Linq;

namespace GitLogVisualizer;

public class RepositoryLog
{
    private readonly RepositoryInfo _repositoryInfo;

    public Signature Me => _repositoryInfo.Me;
    public string Name => _repositoryInfo.Name;
    public IReadOnlyCollection<CommitInfo> Log { get; }

    public RepositoryLog(RepositoryInfo repositoryInfo)
    {
        _repositoryInfo = repositoryInfo;

        Log = _repositoryInfo.AllBranchesInfo.Values
            .SelectMany(x => x.MyCommits.Select(c => new CommitInfo(_repositoryInfo, x.Branch, c)))
            .DistinctBy(x => x.Commit.Sha)
            .OrderByDescending(x => x.When)
            .ToList()
            .AsReadOnly();
    }
}