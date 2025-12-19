using LibGit2Sharp;
using System;

namespace GitLogVisualizer;

public class CommitInfo
{
    private readonly RepositoryInfo _repositoryInfo;
    public string Name => _repositoryInfo.Name;
    public Branch Branch { get; }
    public Commit Commit { get; }
    public DateTimeOffset When => Commit.Author.When;

    public CommitInfo(RepositoryInfo repositoryInfo, Branch branch, Commit commit)
    {
        _repositoryInfo = repositoryInfo;
        Commit = commit;
        Branch = branch;
    }
}