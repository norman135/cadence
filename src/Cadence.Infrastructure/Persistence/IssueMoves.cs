using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Issues;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Infrastructure.Persistence;

/// <summary>
/// A board move in one round trip: the UPDATE's WHERE clause carries every precondition (tenant,
/// project, current status, row version, assignee when the rules need it, project not archived), and
/// RETURNING hands back the new row version. EF's change tracking would need a SELECT first.
/// </summary>
internal sealed class IssueMoves(CadenceDbContext db) : IIssueMoves
{
    public async Task<MovedIssue?> MoveAsync(IssueMove move, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(move);

        var closed = move.ToCategory.IsClosed();
        var category = (int)move.ToCategory;
        var version = move.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // xmin is compared as text: PostgreSQL has no bigint-to-xid cast, and the primary key already
        // narrows the row. ToListAsync runs the statement as written (see IssueNumbers).
        var rows = await db.Database
            .SqlQuery<MovedRow>($"""
                UPDATE issues SET
                    status_id = {move.ToStatusId},
                    status = {category},
                    rank = {move.Rank},
                    updated_at = {move.Now},
                    completed_at = CASE
                        WHEN {closed} THEN CASE WHEN status >= 4 THEN completed_at ELSE {move.Now} END
                        ELSE NULL
                    END
                WHERE id = {move.IssueId}
                    AND organization_id = {move.OrganizationId}
                    AND project_id = {move.ProjectId}
                    AND status_id = {move.FromStatusId}
                    AND xmin::text = {version}
                    AND (NOT {move.CheckAssignee} OR assignee_id IS NOT DISTINCT FROM {move.AssigneeId})
                    AND NOT EXISTS (SELECT 1 FROM projects WHERE id = {move.ProjectId} AND archived_at IS NOT NULL)
                RETURNING xmin::text::bigint AS version, parent_id
                """)
            .ToListAsync(cancellationToken);

        return rows.Count == 1 ? new MovedIssue((uint)rows[0].Version, rows[0].ParentId) : null;
    }

    /// <summary>Columns of RETURNING, named as the snake_case convention expects.</summary>
    private sealed record MovedRow(long Version, Guid? ParentId);
}
