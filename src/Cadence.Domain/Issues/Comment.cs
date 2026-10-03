using Cadence.Domain.Common;

namespace Cadence.Domain.Issues;

/// <summary>A comment on an issue, in Markdown. Editing keeps the previous text as a revision.</summary>
public sealed class Comment : Entity<Guid>, ITenantScoped
{
    public const int MaxBodyLength = 20_000;

    private Comment(Guid id, Guid organizationId, Guid issueId, Guid authorId, string body, DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        IssueId = issueId;
        AuthorId = authorId;
        Body = body;
        CreatedAt = createdAt;
    }

    /// <summary>Used by EF Core.</summary>
    private Comment()
    {
        Body = string.Empty;
    }

    public Guid OrganizationId { get; private init; }

    public Guid IssueId { get; private init; }

    public Guid AuthorId { get; private init; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When the comment was last edited; <see langword="null"/> if never.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    public static Result<Comment> Create(Issue issue, Guid authorId, string body, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var trimmed = body.Trim();
        return IsValidBody(trimmed)
            ? new Comment(Guid.CreateVersion7(), issue.OrganizationId, issue.Id, authorId, trimmed, now)
            : IssueErrors.InvalidCommentBody;
    }

    /// <summary>
    /// Replaces the text. Only the author may edit, and the text being replaced is returned as a
    /// revision, so the edit history is never lost. An unchanged body records nothing.
    /// </summary>
    public Result<CommentRevision?> Edit(Guid editorId, string body, DateTimeOffset now)
    {
        if (editorId != AuthorId)
        {
            return IssueErrors.NotCommentAuthor;
        }

        var trimmed = body.Trim();
        if (!IsValidBody(trimmed))
        {
            return IssueErrors.InvalidCommentBody;
        }

        if (trimmed == Body)
        {
            return Result.Success<CommentRevision?>(null);
        }

        var revision = new CommentRevision(OrganizationId, Id, Body, EditedAt ?? CreatedAt, now);
        Body = trimmed;
        EditedAt = now;
        return revision;
    }

    private static bool IsValidBody(string body) => body.Length is >= 1 and <= MaxBodyLength;
}

/// <summary>A previous version of a comment's text.</summary>
public sealed class CommentRevision : Entity<Guid>, ITenantScoped
{
    internal CommentRevision(Guid organizationId, Guid commentId, string body, DateTimeOffset writtenAt, DateTimeOffset replacedAt)
        : base(Guid.CreateVersion7())
    {
        OrganizationId = organizationId;
        CommentId = commentId;
        Body = body;
        WrittenAt = writtenAt;
        ReplacedAt = replacedAt;
    }

    /// <summary>Used by EF Core.</summary>
    private CommentRevision()
    {
        Body = string.Empty;
    }

    public Guid OrganizationId { get; private init; }

    public Guid CommentId { get; private init; }

    public string Body { get; private init; }

    /// <summary>When this version was written.</summary>
    public DateTimeOffset WrittenAt { get; private init; }

    /// <summary>When it was replaced by an edit.</summary>
    public DateTimeOffset ReplacedAt { get; private init; }
}
