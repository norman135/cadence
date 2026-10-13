using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using Cadence.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Issues;

/// <summary>
/// What the hierarchy rules in <see cref="Issue.MoveUnder"/> need to know. Nesting is at most three
/// levels, so each answer takes at most a few small indexed queries.
/// </summary>
internal static class IssueTree
{
    /// <summary>The parent and its ancestors, closest first.</summary>
    public static async Task<List<Guid>> AncestryAsync(ICadenceDbContext db, Issue parent, CancellationToken cancellationToken)
    {
        var ancestry = new List<Guid> { parent.Id };
        var next = parent.ParentId;

        // One level past the maximum is enough to detect "too deep".
        while (next is { } id && ancestry.Count <= Issue.MaxDepth + 1)
        {
            ancestry.Add(id);
            next = await db.Issues.AsNoTracking()
                .Where(issue => issue.Id == id)
                .Select(issue => issue.ParentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return ancestry;
    }

    /// <summary>Levels of sub-issues below an issue: 0 (none), 1 (children) or 2 (grandchildren).</summary>
    public static async Task<int> SubtreeHeightAsync(ICadenceDbContext db, Guid issueId, CancellationToken cancellationToken)
    {
        if (!await db.Issues.AnyAsync(child => child.ParentId == issueId, cancellationToken))
        {
            return 0;
        }

        var hasGrandchildren = await db.Issues.AnyAsync(
            grandchild => db.Issues.Any(child => child.ParentId == issueId && grandchild.ParentId == child.Id),
            cancellationToken);
        return hasGrandchildren ? 2 : 1;
    }
}

/// <summary>Loads an issue for a change, after checking the caller may write in its project.</summary>
internal static class WritableIssue
{
    public static async Task<Result<(Issue Issue, ProjectRole Role)>> LoadAsync(
        ICadenceDbContext db,
        ProjectGate gate,
        Guid issueId,
        CancellationToken cancellationToken)
    {
        var row = await db.Issues
            .Where(issue => issue.Id == issueId)
            .Select(issue => new
            {
                Issue = issue,
                Archived = db.Projects.Any(project => project.Id == issue.ProjectId && project.ArchivedAt != null),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return IssueErrors.NotFound;
        }

        var role = await gate.RoleInAsync(row.Issue.ProjectId, cancellationToken);
        if (!role.CanRead())
        {
            return IssueErrors.NotFound;
        }

        if (!role.CanWrite())
        {
            return ProjectErrors.Forbidden;
        }

        if (row.Archived)
        {
            return ProjectErrors.Archived;
        }

        return (row.Issue, role!.Value);
    }
}
