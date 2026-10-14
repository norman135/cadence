using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Application.Features.Issues;
using Cadence.Application.Features.Labels;
using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Board;

/// <summary>The epic an issue belongs to (its parent or grandparent), for swimlanes.</summary>
public sealed record EpicSummary(Guid Id, string Key, string Title);

/// <summary>
/// A card on the board: what the card shows, and what a move needs. Send Version back with a move so
/// concurrent changes are detected.
/// </summary>
public sealed record BoardCard(
    Guid Id,
    string Key,
    IssueType Type,
    string Title,
    Guid StatusId,
    IssuePriority Priority,
    string Rank,
    uint Version,
    PersonSummary? Assignee,
    IReadOnlyList<LabelResponse> Labels,
    int? Estimate,
    DateOnly? DueDate,
    int SubIssueCount,
    int SubIssueClosedCount,
    int CommentCount,
    EpicSummary? Epic);

/// <summary>
/// One column: a workflow status and its first cards in rank order. Total counts the column's issues
/// ignoring filters (compared with the WIP limit). HasMore says more cards match than are shown, and
/// NextRank is the rank of the first one not shown, so a card dropped at the end of a partly shown
/// column lands right after the last visible card.
/// </summary>
public sealed record BoardColumn(
    Guid StatusId,
    string Name,
    IssueStatus Category,
    int? WipLimit,
    int Total,
    bool HasMore,
    string? NextRank,
    IReadOnlyList<BoardCard> Cards);

/// <summary>The board. Done and canceled columns show issues closed within the last ClosedWindowDays.</summary>
public sealed record BoardResponse(Guid ProjectId, string ProjectKey, int ClosedWindowDays, IReadOnlyList<BoardColumn> Columns);

/// <summary>
/// A project's board: one column per workflow status, in order, each with its first cards by rank.
/// Quick filters narrow the cards; column totals ignore them.
/// </summary>
public sealed record GetBoardQuery(
    Guid ProjectId,
    Guid[]? AssigneeId = null,
    bool? Unassigned = null,
    IssueType[]? Type = null,
    IssuePriority[]? Priority = null,
    Guid[]? LabelId = null,
    Guid? EpicId = null,
    string? Q = null) : IQuery<Result<BoardResponse>>;

public sealed class GetBoardQueryHandler(ICadenceDbContext db, ProjectGate gate, TimeProvider timeProvider)
    : IQueryHandler<GetBoardQuery, Result<BoardResponse>>
{
    /// <summary>Cards per column. Longer columns end with a link to the full list.</summary>
    public const int ColumnLimit = 100;

    public const int ClosedWindowDays = 14;

    public async ValueTask<Result<BoardResponse>> Handle(GetBoardQuery query, CancellationToken cancellationToken)
    {
        if (!(await gate.RoleInAsync(query.ProjectId, cancellationToken)).CanRead())
        {
            return ProjectErrors.NotFound;
        }

        var closedSince = timeProvider.GetUtcNow().AddDays(-ClosedWindowDays);
        var shown = Filter(db.Issues, query);
        const int take = ColumnLimit + 1;

        // One statement: the columns, each column's total and first cards, and each card's
        // assignee, labels and epic.
        var board = await db.Projects
            .Where(project => project.Id == query.ProjectId)
            .Select(project => new
            {
                project.Key,
                Columns = db.WorkflowStatuses
                    .Where(status => status.ProjectId == project.Id)
                    .OrderBy(status => status.Position)
                    .Select(status => new
                    {
                        status.Id,
                        status.Name,
                        status.Category,
                        status.WipLimit,
                        Total = db.Issues.Count(issue => issue.StatusId == status.Id
                            && (status.Category < IssueStatus.Done || issue.CompletedAt >= closedSince)),
                        Cards = shown
                            .Where(issue => issue.StatusId == status.Id
                                && (status.Category < IssueStatus.Done || issue.CompletedAt >= closedSince))
                            .OrderBy(issue => issue.Rank)
                            .ThenBy(issue => issue.Id)
                            .Take(take)
                            .Select(issue => new CardRow(
                                new BoardCard(
                                    issue.Id,
                                    project.Key + "-" + issue.Number,
                                    issue.Type,
                                    issue.Title,
                                    issue.StatusId,
                                    issue.Priority,
                                    issue.Rank,
                                    issue.Version,
                                    issue.AssigneeId == null
                                        ? null
                                        : new PersonSummary(
                                            issue.AssigneeId.Value,
                                            db.Users.Where(user => user.Id == issue.AssigneeId).Select(user => user.DisplayName).First()),
                                    db.IssueLabels
                                        .Where(issueLabel => issueLabel.IssueId == issue.Id)
                                        .Join(db.Labels, issueLabel => issueLabel.LabelId, label => label.Id, (_, label) => label)
                                        .OrderBy(label => label.Name)
                                        .Select(label => new LabelResponse(label.Id, label.Name, label.Color))
                                        .ToList(),
                                    issue.Estimate,
                                    issue.DueDate,
                                    issue.SubIssueCount,
                                    issue.SubIssueClosedCount,
                                    issue.CommentCount,
                                    null),

                                issue.ParentId,

                                // The epic is the parent or the grandparent. Scalar subqueries keep these
                                // primary-key lookups; asking for whole rows makes EF number every epic of
                                // the organization instead.
                                db.Issues.Where(parent => parent.Id == issue.ParentId).Select(parent => (IssueType?)parent.Type).FirstOrDefault(),
                                db.Issues.Where(parent => parent.Id == issue.ParentId).Select(parent => parent.Number).FirstOrDefault(),
                                db.Issues.Where(parent => parent.Id == issue.ParentId).Select(parent => parent.Title).FirstOrDefault(),
                                db.Issues.Where(parent => parent.Id == issue.ParentId).Select(parent => parent.ParentId).FirstOrDefault(),
                                db.Issues
                                    .Where(grandparent => grandparent.Id == db.Issues.Where(parent => parent.Id == issue.ParentId).Select(parent => parent.ParentId).FirstOrDefault())
                                    .Select(grandparent => (IssueType?)grandparent.Type)
                                    .FirstOrDefault(),
                                db.Issues
                                    .Where(grandparent => grandparent.Id == db.Issues.Where(parent => parent.Id == issue.ParentId).Select(parent => parent.ParentId).FirstOrDefault())
                                    .Select(grandparent => grandparent.Number)
                                    .FirstOrDefault(),
                                db.Issues
                                    .Where(grandparent => grandparent.Id == db.Issues.Where(parent => parent.Id == issue.ParentId).Select(parent => parent.ParentId).FirstOrDefault())
                                    .Select(grandparent => grandparent.Title)
                                    .FirstOrDefault()))
                            .ToList(),
                    })
                    .ToList(),
            })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (board is null)
        {
            return ProjectErrors.NotFound;
        }

        return new BoardResponse(
            query.ProjectId,
            board.Key,
            ClosedWindowDays,
            [
                .. board.Columns.Select(column => new BoardColumn(
                    column.Id,
                    column.Name,
                    column.Category,
                    column.WipLimit,
                    column.Total,
                    column.Cards.Count > ColumnLimit,
                    column.Cards.Count > ColumnLimit ? column.Cards[ColumnLimit].Card.Rank : null,
                    [.. column.Cards.Take(ColumnLimit).Select(row => row.Card with { Epic = row.Epic(board.Key) })])),
            ]);
    }

    /// <summary>A card as read, with its parent and grandparent, one of which may be its epic.</summary>
    private sealed record CardRow(
        BoardCard Card,
        Guid? ParentId,
        IssueType? ParentType,
        int ParentNumber,
        string? ParentTitle,
        Guid? GrandparentId,
        IssueType? GrandparentType,
        int GrandparentNumber,
        string? GrandparentTitle)
    {
        public EpicSummary? Epic(string projectKey) =>
            ParentType == IssueType.Epic && ParentId is { } parentId
                ? new EpicSummary(parentId, IssueKeys.Format(projectKey, ParentNumber), ParentTitle ?? string.Empty)
                : GrandparentType == IssueType.Epic && GrandparentId is { } grandparentId
                    ? new EpicSummary(grandparentId, IssueKeys.Format(projectKey, GrandparentNumber), GrandparentTitle ?? string.Empty)
                    : null;
    }

    private IQueryable<Issue> Filter(IQueryable<Issue> issues, GetBoardQuery query)
    {
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

        if (query.Type is { Length: > 0 } types)
        {
            issues = issues.Where(issue => types.Contains(issue.Type));
        }

        if (query.Priority is { Length: > 0 } priorities)
        {
            issues = issues.Where(issue => priorities.Contains(issue.Priority));
        }

        if (query.LabelId is { Length: > 0 } labels)
        {
            issues = issues.Where(issue => db.IssueLabels.Any(issueLabel => issueLabel.IssueId == issue.Id && labels.Contains(issueLabel.LabelId)));
        }

        if (query.EpicId is { } epicId)
        {
            issues = issues.Where(issue => issue.ParentId == epicId
                || db.Issues.Any(middle => middle.Id == issue.ParentId && middle.ParentId == epicId));
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var lowered = query.Q.Trim().ToLowerInvariant();
#pragma warning disable CA1862 // EF Core translates ToLower() to SQL lower(); StringComparison overloads aren't translatable.
            issues = issues.Where(issue => issue.Title.ToLower().Contains(lowered));
#pragma warning restore CA1862
        }

        return issues;
    }
}

/// <summary>Where a card ended up, and its new version for the next move.</summary>
public sealed record MovedCardResponse(Guid IssueId, Guid StatusId, IssueStatus Category, string Rank, uint Version);

/// <summary>
/// Moves a card: to a status (possibly the same one) and a rank between its new neighbours. The
/// client sends what it saw: the status the card was in (FromStatusId), its version and its assignee
/// (transition rules may depend on it). If the issue changed since, the move is refused with 409 and
/// the client refreshes.
/// </summary>
public sealed record MoveCardCommand(
    Guid ProjectId,
    Guid IssueId,
    Guid FromStatusId,
    Guid ToStatusId,
    string Rank,
    uint Version,
    Guid? AssigneeId) : ICommand<Result<MovedCardResponse>>;

public sealed class MoveCardCommandValidator : AbstractValidator<MoveCardCommand>
{
    public MoveCardCommandValidator() =>
        RuleFor(command => command.Rank).Must(Rank.IsValid).WithMessage("That position in the order isn't valid.");
}

public sealed class MoveCardCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IWorkflowCache workflows,
    IIssueMoves moves,
    TimeProvider timeProvider)
    : ICommandHandler<MoveCardCommand, Result<MovedCardResponse>>
{
    public async ValueTask<Result<MovedCardResponse>> Handle(MoveCardCommand command, CancellationToken cancellationToken)
    {
        var role = await gate.RoleInAsync(command.ProjectId, cancellationToken);
        if (!role.CanRead())
        {
            return ProjectErrors.NotFound;
        }

        if (!role.CanWrite())
        {
            return ProjectErrors.Forbidden;
        }

        var workflow = (await workflows.GetAsync(tenant.OrganizationId, command.ProjectId, cancellationToken))?.ToDomain(tenant.OrganizationId);
        if (workflow?.FindStatus(command.ToStatusId) is not { } to || workflow.FindStatus(command.FromStatusId) is not { } from)
        {
            return WorkflowErrors.UnknownStatus;
        }

        var allowed = workflow.CanMove(from.Id, to.Id, command.AssigneeId, currentUser.RequiredUserId, role.GetValueOrDefault());
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        // The whole move is one UPDATE, guarded by what the client saw. Zero rows means something
        // differs, and only then do we look at the issue to say what.
        var moved = await moves.MoveAsync(
            new IssueMove(
                tenant.OrganizationId,
                command.ProjectId,
                command.IssueId,
                from.Id,
                to.Id,
                to.Category,
                command.Rank,
                command.Version,
                workflow.MoveDependsOnAssignee(from.Id, to.Id),
                command.AssigneeId,
                timeProvider.GetUtcNow()),
            cancellationToken);
        if (moved is null)
        {
            return await ExplainAsync(command, cancellationToken);
        }

        if (from.Category.IsClosed() != to.Category.IsClosed())
        {
            await db.RefreshOpenIssuesAsync(command.ProjectId, cancellationToken);
            await db.RefreshSubIssuesAsync(moved.ParentId, cancellationToken);
        }

        return new MovedCardResponse(command.IssueId, to.Id, to.Category, command.Rank, moved.Version);
    }

    private async Task<Error> ExplainAsync(MoveCardCommand command, CancellationToken cancellationToken)
    {
        var current = await db.Issues
            .Where(issue => issue.Id == command.IssueId && issue.ProjectId == command.ProjectId)
            .Select(issue => new { Archived = db.Projects.Any(project => project.Id == issue.ProjectId && project.ArchivedAt != null) })
            .SingleOrDefaultAsync(cancellationToken);

        return current switch
        {
            null => IssueErrors.NotFound,
            { Archived: true } => ProjectErrors.Archived,
            _ => IssueErrors.Conflict,
        };
    }
}
