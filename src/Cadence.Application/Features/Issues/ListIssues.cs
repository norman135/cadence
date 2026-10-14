using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Labels;
using Cadence.Domain.Issues;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Issues;

public enum IssueSort
{
    /// <summary>Recently updated first (the default).</summary>
    Updated,

    /// <summary>Newest first.</summary>
    Created,

    /// <summary>Most urgent first.</summary>
    Priority,

    /// <summary>Soonest due first; issues without a due date last.</summary>
    DueDate,
}

/// <summary>
/// Issues the caller can see, filtered and sorted on the server, one keyset page at a time. Filters of
/// the same kind are alternatives (status Todo or In progress); different kinds all apply.
/// </summary>
/// <param name="ProjectId">Only these projects.</param>
/// <param name="Status">Only these status categories.</param>
/// <param name="StatusId">Only these workflow statuses.</param>
/// <param name="Priority">Only these priorities.</param>
/// <param name="Type">Only these types.</param>
/// <param name="AssigneeId">Only issues assigned to these people.</param>
/// <param name="Unassigned">Also (or only, without <paramref name="AssigneeId"/>) unassigned issues.</param>
/// <param name="LabelId">Only issues with at least one of these labels.</param>
/// <param name="ParentId">Only sub-issues of this issue.</param>
/// <param name="Q">Words in the title, or an issue key such as ATL-142.</param>
/// <param name="Sort">The order (default: recently updated).</param>
/// <param name="After">The cursor from the previous page.</param>
/// <param name="Limit">Page size, 1 to 100 (default 50).</param>
public sealed record ListIssuesQuery(
    Guid[]? ProjectId = null,
    IssueStatus[]? Status = null,
    Guid[]? StatusId = null,
    IssuePriority[]? Priority = null,
    IssueType[]? Type = null,
    Guid[]? AssigneeId = null,
    bool? Unassigned = null,
    Guid[]? LabelId = null,
    Guid? ParentId = null,
    string? Q = null,
    IssueSort? Sort = null,
    string? After = null,
    int? Limit = null) : IQuery<KeysetPage<IssueListItem>>;

public sealed class ListIssuesQueryHandler(ICadenceDbContext db, ProjectGate gate)
    : IQueryHandler<ListIssuesQuery, KeysetPage<IssueListItem>>
{
    public async ValueTask<KeysetPage<IssueListItem>> Handle(ListIssuesQuery query, CancellationToken cancellationToken)
    {
        var limit = KeysetPaging.ClampLimit(query.Limit);
        var sort = query.Sort ?? IssueSort.Updated;
        var issues = Filter(db.Issues.AsNoTracking(), query, await gate.VisibleProjectIdsAsync(cancellationToken));

        if (IssueCursor.TryRead(query.After, out var cursor))
        {
            // Rows after the cursor in the active order; the id breaks ties.
            var id = cursor.Id;
            switch (sort)
            {
                case IssueSort.Created:
                    issues = issues.Where(issue => issue.Id < id);
                    break;
                case IssueSort.Priority:
                    var priority = (IssuePriority)cursor.Value;
                    issues = issues.Where(issue => issue.Priority < priority || (issue.Priority == priority && issue.Id < id));
                    break;
                case IssueSort.DueDate:
                    var due = DateOnly.FromDayNumber((int)cursor.Value);
                    issues = issues.Where(issue =>
                        (issue.DueDate ?? DateOnly.MaxValue) > due || ((issue.DueDate ?? DateOnly.MaxValue) == due && issue.Id < id));
                    break;
                default:
                    var updated = new DateTimeOffset(cursor.Value, TimeSpan.Zero);
                    issues = issues.Where(issue => issue.UpdatedAt < updated || (issue.UpdatedAt == updated && issue.Id < id));
                    break;
            }
        }

        var ordered = sort switch
        {
            IssueSort.Created => issues.OrderByDescending(issue => issue.Id),
            IssueSort.Priority => issues.OrderByDescending(issue => issue.Priority).ThenByDescending(issue => issue.Id),
            IssueSort.DueDate => issues.OrderBy(issue => issue.DueDate ?? DateOnly.MaxValue).ThenByDescending(issue => issue.Id),
            _ => issues.OrderByDescending(issue => issue.UpdatedAt).ThenByDescending(issue => issue.Id),
        };

        // One statement: the page, its project keys, assignee names and labels.
        var rows = await ordered
            .Take(limit + 1)
            .Select(issue => new IssueListItem(
                issue.Id,
                db.Projects.Where(project => project.Id == issue.ProjectId).Select(project => project.Key).First() + "-" + issue.Number,
                issue.ProjectId,
                issue.Type,
                issue.Title,
                issue.Status,
                issue.StatusId,
                issue.Priority,
                issue.AssigneeId == null
                    ? null
                    : new PersonSummary(
                        issue.AssigneeId.Value,
                        db.Users.Where(user => user.Id == issue.AssigneeId).Select(user => user.DisplayName).First()),
                issue.Estimate,
                issue.DueDate,
                db.IssueLabels
                    .Where(issueLabel => issueLabel.IssueId == issue.Id)
                    .Join(db.Labels, issueLabel => issueLabel.LabelId, label => label.Id, (_, label) => label)
                    .OrderBy(label => label.Name)
                    .Select(label => new LabelResponse(label.Id, label.Name, label.Color))
                    .ToList(),
                issue.SubIssueCount,
                issue.SubIssueClosedCount,
                issue.CommentCount,
                issue.CreatedAt,
                issue.UpdatedAt))
            .ToListAsync(cancellationToken);

        return KeysetPaging.ToPage(rows, limit, last => IssueCursor.Write(sort, last));
    }

    private IQueryable<Issue> Filter(IQueryable<Issue> issues, ListIssuesQuery query, IReadOnlyList<Guid>? visibleProjects)
    {
        if (visibleProjects is not null)
        {
            issues = issues.Where(issue => visibleProjects.Contains(issue.ProjectId));
        }

        if (query.ProjectId is { Length: > 0 } projects)
        {
            issues = issues.Where(issue => projects.Contains(issue.ProjectId));
        }

        if (query.StatusId is { Length: > 0 } statusIds)
        {
            issues = issues.Where(issue => statusIds.Contains(issue.StatusId));
        }

        if (query.Status is { Length: > 0 } statuses)
        {
            issues = issues.Where(issue => statuses.Contains(issue.Status));
        }

        if (query.Priority is { Length: > 0 } priorities)
        {
            issues = issues.Where(issue => priorities.Contains(issue.Priority));
        }

        if (query.Type is { Length: > 0 } types)
        {
            issues = issues.Where(issue => types.Contains(issue.Type));
        }

        var assignees = query.AssigneeId ?? [];
        if (assignees.Length > 0 && query.Unassigned == true)
        {
            issues = issues.Where(issue => issue.AssigneeId == null || assignees.Contains(issue.AssigneeId.Value));
        }
        else if (assignees.Length > 0)
        {
            issues = issues.Where(issue => issue.AssigneeId != null && assignees.Contains(issue.AssigneeId.Value));
        }
        else if (query.Unassigned == true)
        {
            issues = issues.Where(issue => issue.AssigneeId == null);
        }

        if (query.LabelId is { Length: > 0 } labels)
        {
            issues = issues.Where(issue => db.IssueLabels.Any(issueLabel => issueLabel.IssueId == issue.Id && labels.Contains(issueLabel.LabelId)));
        }

        if (query.ParentId is { } parentId)
        {
            issues = issues.Where(issue => issue.ParentId == parentId);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var text = query.Q.Trim();
            if (IssueKeys.TryParse(text, out var projectKey, out var number))
            {
                issues = issues.Where(issue => issue.Number == number
                    && db.Projects.Any(project => project.Id == issue.ProjectId && project.Key == projectKey));
            }
            else
            {
                var lowered = text.ToLowerInvariant();
#pragma warning disable CA1862 // EF Core translates ToLower() to SQL lower(); StringComparison overloads aren't translatable.
                issues = issues.Where(issue => issue.Title.ToLower().Contains(lowered));
#pragma warning restore CA1862
            }
        }

        return issues;
    }
}

/// <summary>
/// The position after the last row of a page, for the active sort: the sort value and the issue id
/// as a tie-breaker, encoded as an opaque URL-safe string.
/// </summary>
internal readonly record struct IssueCursor(long Value, Guid Id)
{
    public static string Write(IssueSort sort, IssueListItem last)
    {
        var value = sort switch
        {
            IssueSort.Created => 0,
            IssueSort.Priority => (long)last.Priority,
            IssueSort.DueDate => (last.DueDate ?? DateOnly.MaxValue).DayNumber,
            _ => last.UpdatedAt.UtcTicks,
        };

        var text = string.Create(CultureInfo.InvariantCulture, $"{value}:{last.Id:N}");
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(text));
    }

    public static bool TryRead(string? cursor, out IssueCursor result)
    {
        result = default;
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split(':');
            if (parts.Length == 2
                && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                && Guid.TryParseExact(parts[1], "N", out var id))
            {
                result = new IssueCursor(value, id);
                return true;
            }
        }
        catch (FormatException)
        {
            // A malformed cursor starts from the first page, like no cursor.
        }

        return false;
    }
}
