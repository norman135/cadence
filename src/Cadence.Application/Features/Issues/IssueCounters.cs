using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Issues;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Issues;

/// <summary>
/// Keeps the denormalized counters right after issues change: a parent's sub-issue counts, a project's
/// open-issue count and an issue's comment count. Each is recomputed from the rows in one statement,
/// so concurrent changes can't drift them.
/// </summary>
public static class IssueCounters
{
    public static async Task RefreshSubIssuesAsync(this ICadenceDbContext db, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is not { } id)
        {
            return;
        }

        await db.Issues
            .Where(parent => parent.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(parent => parent.SubIssueCount, parent => db.Issues.Count(child => child.ParentId == parent.Id))
                    .SetProperty(
                        parent => parent.SubIssueClosedCount,
                        parent => db.Issues.Count(child => child.ParentId == parent.Id && child.Status >= IssueStatus.Done)),
                cancellationToken);
    }

    /// <summary>Sub-issue counts of every parent in the project, after many statuses changed at once.</summary>
    public static Task RefreshAllSubIssuesAsync(this ICadenceDbContext db, Guid projectId, CancellationToken cancellationToken) =>
        db.Issues
            .Where(parent => parent.ProjectId == projectId && parent.SubIssueCount > 0)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    parent => parent.SubIssueClosedCount,
                    parent => db.Issues.Count(child => child.ParentId == parent.Id && child.Status >= IssueStatus.Done)),
                cancellationToken);

    public static Task RefreshOpenIssuesAsync(this ICadenceDbContext db, Guid projectId, CancellationToken cancellationToken) =>
        db.Projects
            .Where(project => project.Id == projectId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    project => project.OpenIssueCount,
                    project => db.Issues.Count(issue => issue.ProjectId == project.Id && issue.Status < IssueStatus.Done)),
                cancellationToken);

    public static Task RefreshCommentsAsync(this ICadenceDbContext db, Guid issueId, CancellationToken cancellationToken) =>
        db.Issues
            .Where(issue => issue.Id == issueId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(issue => issue.CommentCount, issue => db.Comments.Count(comment => comment.IssueId == issue.Id)),
                cancellationToken);
}
