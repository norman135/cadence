using Cadence.Domain.Common;

namespace Cadence.Domain.Issues;

/// <summary>
/// How two issues relate. Stored once, from source to target; the target sees the inverse
/// ("blocked by", "duplicated by"), and "relates to" reads the same both ways.
/// </summary>
public enum IssueLinkType
{
    Blocks,
    RelatesTo,
    Duplicates,
}

public sealed class IssueLink : Entity<Guid>, ITenantScoped
{
    private IssueLink(Guid id, Guid organizationId, Guid sourceIssueId, Guid targetIssueId, IssueLinkType type, Guid createdByUserId, DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        SourceIssueId = sourceIssueId;
        TargetIssueId = targetIssueId;
        Type = type;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    /// <summary>Used by EF Core.</summary>
    private IssueLink()
    {
    }

    public Guid OrganizationId { get; private init; }

    public Guid SourceIssueId { get; private init; }

    public Guid TargetIssueId { get; private init; }

    public IssueLinkType Type { get; private init; }

    public Guid CreatedByUserId { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>
    /// Links two issues of the same organization (they may be in different projects). "Relates to" is
    /// symmetric, so it is stored with the lower id first and can't be duplicated in reverse.
    /// </summary>
    public static Result<IssueLink> Create(Issue source, Issue target, IssueLinkType type, Guid createdByUserId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (source.Id == target.Id)
        {
            return IssueErrors.LinkToSelf;
        }

        var (from, to) = type == IssueLinkType.RelatesTo && target.Id.CompareTo(source.Id) < 0
            ? (target, source)
            : (source, target);

        return new IssueLink(Guid.CreateVersion7(), source.OrganizationId, from.Id, to.Id, type, createdByUserId, now);
    }
}
