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
        if (tag is null)
            throw new ArgumentNullException(nameof(tag));

        if (tag.Target is not Commit commit)
            throw new ArgumentException(
                $"Tag '{tag.FriendlyName}' does not point to a commit (target type: {tag.Target?.GetType().Name ?? "null"}).",
                nameof(tag));

        Tag = tag;
        Commit = commit;
        When = Commit.Author.When;
    }
}