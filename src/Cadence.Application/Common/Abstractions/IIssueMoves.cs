using Cadence.Domain.Issues;

namespace Cadence.Application.Common.Abstractions;

/// <summary>
/// A card move as one guarded UPDATE: it applies only if the issue is as the client saw it.
/// CheckAssignee says whether the assignee must still be AssigneeId (when transition rules used it).
/// </summary>
public sealed record IssueMove(
    Guid OrganizationId,
    Guid ProjectId,
    Guid IssueId,
    Guid FromStatusId,
    Guid ToStatusId,
    IssueStatus ToCategory,
    string Rank,
    uint Version,
    bool CheckAssignee,
    Guid? AssigneeId,
    DateTimeOffset Now);

/// <summary>The moved issue's new row version, and its parent (whose sub-issue counts may change).</summary>
public sealed record MovedIssue(uint Version, Guid? ParentId);

/// <summary>Moves issues on the board.</summary>
public interface IIssueMoves
{
    /// <summary>
    /// Applies the move in a single statement, or returns <see langword="null"/> when the issue isn't
    /// in the expected state (another version, status or assignee), is gone, or its project is archived.
    /// </summary>
    Task<MovedIssue?> MoveAsync(IssueMove move, CancellationToken cancellationToken);
}
