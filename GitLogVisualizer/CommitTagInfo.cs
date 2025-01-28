using LibGit2Sharp;
using System;

namespace GitLogVisualizer;

public class CommitTagInfo
{
    public Tag Tag { get; }
    public DateTimeOffset When { get; }
    public Commit Commit { get; }

    public CommitTagInfo(Tag tag)
    {
        Tag = tag;
        Commit = (Commit)tag.Target;
        When = Commit.Author.When;
    }
}