using FEx.Common.Extensions;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    public string Url => OriginRemote.Url;
    public Remote OriginRemote => Repo.Network.Remotes.First();
    public Configuration Config => Repo.Config;
    public Branch CurrentBranch => Repo.Head;

    private RepositoryInfo(DirectoryInfo directory, DateTime? loadCommitsAfter)
    {
        _loadCommitsAfter = loadCommitsAfter;
        Name = directory.Name;
        Repo = new(directory.FullName);
        Me = Config.BuildSignature(DateTimeOffset.Now);

        var allBranches = Repo.Branches.Select(LoadBranchInfo).ToList();

        MainBranchInfo = allBranches.Single(branch =>
            branch.CurrentBranchName.EndsWith("/master") || branch.CurrentBranchName.EndsWith("/main"));

        AllBranchesInfo = allBranches
            .Where(b => b.Commits.Any())
            .ToDictionary(b => b.Branch.CanonicalName, b => b);
    }

    public static RepositoryInfo GetRepositoryInformationForPath(DirectoryInfo repositoryDirectory,
                                                                 DateTime? loadCommitsAfter = null)
    {
        repositoryDirectory.Guard(nameof(repositoryDirectory));

        if (!Repository.IsValid(repositoryDirectory.FullName))
            throw new($"{repositoryDirectory.FullName} is not valid Git repository path");

        return new(repositoryDirectory, loadCommitsAfter);
    }

    public BranchInfo LoadBranchInfo(Branch branch) => new(branch, this, _loadCommitsAfter);

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
        {
            Repo?.Dispose();
            OriginRemote?.Dispose();
            Config?.Dispose();
        }

        _isDisposed = true;
    }
    #endregion
}