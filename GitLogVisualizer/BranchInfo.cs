using FEx.Agnostics.Abstractions.Extensions;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GitLogVisualizer;

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class BranchInfo
{
    public Branch Branch { get; }

    public Commit LastCommit => Branch.Tip;
    public string LastCommitHash => LastCommit.Sha;
    public string CurrentBranchName => Branch.FriendlyName;
    public bool HasUnpushedCommits => Branch.TrackingDetails.AheadBy > 0;

    public string TrackedBranchName =>
        Branch.IsTracking
            ? Branch.TrackedBranch.CanonicalName
            : null;

    public IReadOnlyCollection<Commit> Commits { get; }
    public HashSet<string> CommitsHashes { get; }
    public IReadOnlyCollection<CommitTagInfo> Tags { get; }
    public IReadOnlyCollection<Commit> MyCommits { get; }
    public RepositoryInfo RepositoryInfo { get; }
    private string DebuggerDisplay => CurrentBranchName;

    private Repository Repository => RepositoryInfo.Repo;
    private Signature Me => RepositoryInfo.Me;

    public BranchInfo(Branch branch, RepositoryInfo repositoryInfo, DateTime? loadCommitsAfter, Branch baseBranch = null)
    {
        RepositoryInfo = repositoryInfo;

        Branch = branch;
        Commits = GetBranchLog(loadCommitsAfter, baseBranch);
        CommitsHashes = [.. Commits.Select(commit => commit.Sha)];

        Tags = Repository.Tags.Where(tag => tag.Target is Commit commit && CommitsHashes.Contains(commit.Sha))
            .Select(tag => new CommitTagInfo(tag))
            .OrderByDescending(tagInfo => tagInfo.When)
            .ToList()
            .AsReadOnly();

        MyCommits = Commits.Where(IsMine).ToList().AsReadOnly();
    }

    private static bool MatchCommit(Commit commit, DateTime? loadCommitsAfter) =>
        !loadCommitsAfter.HasValue || commit.Author.When.LocalDateTime.Date.IsAfterOrEqual(loadCommitsAfter.Value);

    private bool IsMine(Commit commit) =>
        commit.Author.Name.Equals(Me.Name, StringComparison.OrdinalIgnoreCase)
        || commit.Author.Email.Equals(Me.Email, StringComparison.OrdinalIgnoreCase);

    private IReadOnlyCollection<Commit> GetBranchLog(DateTime? loadCommitsAfter, Branch baseBranch)
    {
        var excludeFrom = baseBranch != null && Branch.CanonicalName != baseBranch.CanonicalName
            ? baseBranch
            : null;

        return Repository.Commits.QueryBy(new CommitFilter
            {
                IncludeReachableFrom = Branch,
                ExcludeReachableFrom = excludeFrom,
                SortBy = CommitSortStrategies.Time,
                FirstParentOnly = true
            })
            .Where(commit => MatchCommit(commit, loadCommitsAfter))
            .ToList()
            .AsReadOnly();
    }
}