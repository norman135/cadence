using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Application.Features.Labels;
using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Issues;

/// <summary>An issue by its key (ATL-142), with everything its page shows except the comments.</summary>
public sealed record GetIssueQuery(string Key) : IQuery<Result<IssueDetail>>;

public sealed class GetIssueQueryHandler(ICadenceDbContext db, ProjectGate gate)
    : IQueryHandler<GetIssueQuery, Result<IssueDetail>>
{
    public async ValueTask<Result<IssueDetail>> Handle(GetIssueQuery query, CancellationToken cancellationToken)
    {
        if (!IssueKeys.TryParse(query.Key, out var projectKey, out var number))
        {
            return IssueErrors.NotFound;
        }

        // 1: the issue with its project, people, labels and parent.
        var core = await (
            from issue in db.Issues.AsNoTracking()
            join project in db.Projects on issue.ProjectId equals project.Id
            where project.Key == projectKey && issue.Number == number
            select new
            {
                Issue = issue,
                ProjectKey = project.Key,
                ProjectName = project.Name,
                AssigneeName = db.Users.Where(user => user.Id == issue.AssigneeId).Select(user => user.DisplayName).FirstOrDefault(),
                ReporterName = db.Users.Where(user => user.Id == issue.ReporterId).Select(user => user.DisplayName).First(),
                Labels = db.IssueLabels
                    .Where(issueLabel => issueLabel.IssueId == issue.Id)
                    .Join(db.Labels, issueLabel => issueLabel.LabelId, label => label.Id, (_, label) => label)
                    .OrderBy(label => label.Name)
                    .Select(label => new LabelResponse(label.Id, label.Name, label.Color))
                    .ToList(),
                Parent = db.Issues
                    .Where(parent => parent.Id == issue.ParentId)
                    .Select(parent => new IssueReference(parent.Id, project.Key + "-" + parent.Number, parent.Title, parent.Status, parent.Type))
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (core is null || await gate.RoleInAsync(core.Issue.ProjectId, cancellationToken) is not { } role)
        {
            return IssueErrors.NotFound;
        }

        var issueId = core.Issue.Id;

        // 2: sub-issues, in creation order.
        var subIssues = await db.Issues.AsNoTracking()
            .Where(child => child.ParentId == issueId)
            .OrderBy(child => child.Number)
            .Select(child => new IssueReference(child.Id, core.ProjectKey + "-" + child.Number, child.Title, child.Status, child.Type))
            .ToListAsync(cancellationToken);

        // 3: links from either side, with the other issue. It may be in another project; links to
        // projects the caller can't see are left out, so their titles don't leak.
        var visible = await gate.VisibleProjectIdsAsync(cancellationToken);
        var otherIssues = db.Issues.AsNoTracking();
        if (visible is not null)
        {
            otherIssues = otherIssues.Where(other => visible.Contains(other.ProjectId));
        }

        var links = await (
            from link in db.IssueLinks.AsNoTracking()
            where link.SourceIssueId == issueId || link.TargetIssueId == issueId
            let otherId = link.SourceIssueId == issueId ? link.TargetIssueId : link.SourceIssueId
            join other in otherIssues on otherId equals other.Id
            join otherProject in db.Projects on other.ProjectId equals otherProject.Id
            orderby link.Id
            select new
            {
                link.Id,
                link.Type,
                Outgoing = link.SourceIssueId == issueId,
                Other = new IssueReference(other.Id, otherProject.Key + "-" + other.Number, other.Title, other.Status, other.Type),
            })
            .ToListAsync(cancellationToken);

        var issueRow = core.Issue;
        return new IssueDetail(
            issueRow.Id,
            IssueKeys.Format(core.ProjectKey, issueRow.Number),
            issueRow.ProjectId,
            core.ProjectName,
            role,
            issueRow.Type,
            issueRow.Title,
            issueRow.Description,
            issueRow.Status,
            issueRow.StatusId,
            issueRow.Priority,
            issueRow.AssigneeId is { } assigneeId ? new PersonSummary(assigneeId, core.AssigneeName ?? string.Empty) : null,
            new PersonSummary(issueRow.ReporterId, core.ReporterName),
            issueRow.Estimate,
            issueRow.DueDate,
            core.Labels,
            core.Parent,
            subIssues,
            [.. links.Select(link => new IssueLinkResponse(link.Id, Relation(link.Type, link.Outgoing), link.Other))],
            issueRow.CommentCount,
            issueRow.CreatedAt,
            issueRow.UpdatedAt,
            issueRow.CompletedAt,
            issueRow.Version);
    }

    /// <summary>The link as read from this issue's side.</summary>
    private static string Relation(IssueLinkType type, bool outgoing) => (type, outgoing) switch
    {
        (IssueLinkType.Blocks, true) => "blocks",
        (IssueLinkType.Blocks, false) => "blocked_by",
        (IssueLinkType.Duplicates, true) => "duplicates",
        (IssueLinkType.Duplicates, false) => "duplicated_by",
        _ => "relates_to",
    };
}
