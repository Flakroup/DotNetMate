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

    /// <summary>
    /// When set, overrides the branch name derived from <see cref="Branch"/>.
    /// Used for commits recovered from deleted branches via merge commit messages.
    /// </summary>
    public string BranchNameOverride { get; }

    public CommitInfo(RepositoryInfo repositoryInfo, Branch branch, Commit commit, string branchNameOverride = null)
    {
        _repositoryInfo = repositoryInfo;
        Commit = commit;
        Branch = branch;
        BranchNameOverride = branchNameOverride;
    }
}