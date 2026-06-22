using FEx.Agnostics.Abstractions.Extensions;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GitLogVisualizer;

public class RepositoryInfo : IDisposable
{
    private readonly DateTime? _loadCommitsAfter;
    private bool _isDisposed;

    public Repository Repo { get; }
    public BranchInfo MainBranchInfo { get; }
    public BranchInfo CurrentBranchInfo { get; }
    public IDictionary<string, BranchInfo> AllBranchesInfo { get; }
    public Signature Me { get; }
    public string Name { get; }

    public bool HasUncommittedChanges => Repo.RetrieveStatus().Any(s => s.State != FileStatus.Ignored);
    public string Url => OriginRemote?.Url;
    public Remote OriginRemote => Repo.Network.Remotes.FirstOrDefault();
    public Configuration Config => Repo.Config;
    public Branch CurrentBranch => Repo.Head;

    private static readonly string[] BaseBranchNames = ["develop", "development", "main", "master"];

    private RepositoryInfo(DirectoryInfo directory, DateTime? loadCommitsAfter)
    {
        _loadCommitsAfter = loadCommitsAfter;
        Name = directory.Name;
        Repo = new(directory.FullName);
        Me = Config.BuildSignature(DateTimeOffset.Now);

        var branches = Repo.Branches
            .Where(b => b.FriendlyName != "origin/HEAD")
            .ToList();

        var baseBranch = FindBaseBranch(branches);

        var allBranches = branches.Select(b => LoadBranchInfo(b, baseBranch)).ToList();

        MainBranchInfo = allBranches.FirstOrDefault(branch =>
            branch.CurrentBranchName.EndsWith("/master") || branch.CurrentBranchName.EndsWith("/main"));

        AllBranchesInfo = allBranches.Where(b => b.Commits.Any()).ToDictionary(b => b.Branch.CanonicalName, b => b);
    }

    private static Branch FindBaseBranch(IEnumerable<Branch> branches) =>
        BaseBranchNames
            .Select(name => branches.FirstOrDefault(b =>
                b.FriendlyName.Equals(name, StringComparison.OrdinalIgnoreCase)
                || b.FriendlyName.Equals($"origin/{name}", StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(b => b != null);

    public static RepositoryInfo GetRepositoryInformationForPath(DirectoryInfo repositoryDirectory,
                                                                 DateTime? loadCommitsAfter = null)
    {
        repositoryDirectory.Guard(nameof(repositoryDirectory));

        if (!Repository.IsValid(repositoryDirectory.FullName))
            throw new InvalidOperationException($"{repositoryDirectory.FullName} is not valid Git repository path");

        return new(repositoryDirectory, loadCommitsAfter);
    }

    public BranchInfo LoadBranchInfo(Branch branch, Branch baseBranch = null) => new(branch, this, _loadCommitsAfter, baseBranch);

    /// <summary>
    /// Recovers my commits from deleted feature branches by walking the second parent
    /// of merge commits on the base branch. Uses the merge commit message to determine
    /// the original branch name.
    /// </summary>
    public IEnumerable<CommitInfo> GetOrphanedMergedCommits()
    {
        var baseBranch = FindBaseBranch(Repo.Branches.ToList());

        if (baseBranch == null)
            yield break;

        var existingTips = Repo.Branches
            .Select(b => b.Tip?.Sha)
            .Where(sha => sha != null)
            .ToHashSet();

        var mergeCommits = Repo.Commits.QueryBy(new CommitFilter
            {
                IncludeReachableFrom = baseBranch,
                FirstParentOnly = true
            })
            .Where(c => c.Parents.Count() > 1);

        foreach (var mergeCommit in mergeCommits)
        {
            var secondParent = mergeCommit.Parents.ElementAt(1);

            // Branch still exists — handled via normal branch iteration
            if (existingTips.Contains(secondParent.Sha))
                continue;

            var branchName = TryExtractMergedBranchName(mergeCommit.Message);

            if (branchName == null)
                continue;

            var featureCommits = Repo.Commits.QueryBy(new CommitFilter
                {
                    IncludeReachableFrom = secondParent,
                    ExcludeReachableFrom = mergeCommit.Parents.First(),
                    FirstParentOnly = true
                })
                .Where(c => IsMine(c) && (_loadCommitsAfter == null || c.Author.When.LocalDateTime.Date >= _loadCommitsAfter.Value.Date));

            foreach (var commit in featureCommits)
                yield return new CommitInfo(this, baseBranch, commit, branchName);
        }
    }

    private bool IsMine(Commit commit) =>
        commit.Author.Name.Equals(Me.Name, StringComparison.OrdinalIgnoreCase)
        || commit.Author.Email.Equals(Me.Email, StringComparison.OrdinalIgnoreCase);

    private static string TryExtractMergedBranchName(string message)
    {
        var match = Regex.Match(message, "Merge branch '([^']+)'", RegexOptions.None, TimeSpan.FromMilliseconds(100));

        return match.Success
            ? match.Groups[1].Value.Split('/').Last()
            : null;
    }

    #region IDisposable
    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
            // Only dispose the repository itself
            // OriginRemote and Config are owned by the repository and will be disposed with it
            Repo?.Dispose();

        _isDisposed = true;
    }
    #endregion
}