using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Application.Features.Issues;
using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Workflows;

/// <summary>A project's workflow. Anyone who can see the project can read it.</summary>
public sealed record GetWorkflowQuery(Guid ProjectId) : IQuery<Result<WorkflowResponse>>;

public sealed class GetWorkflowQueryHandler(ProjectGate gate, ITenantContext tenant, IWorkflowCache workflows)
    : IQueryHandler<GetWorkflowQuery, Result<WorkflowResponse>>
{
    public async ValueTask<Result<WorkflowResponse>> Handle(GetWorkflowQuery query, CancellationToken cancellationToken)
    {
        if (!(await gate.RoleInAsync(query.ProjectId, cancellationToken)).CanRead())
        {
            return ProjectErrors.NotFound;
        }

        var workflow = await workflows.GetAsync(tenant.OrganizationId, query.ProjectId, cancellationToken);
        return workflow is null ? ProjectErrors.NotFound : workflow;
    }
}

/// <summary>A status in the workflow editor. Existing statuses keep their id; new ones have none.</summary>
public sealed record WorkflowStatusInput(Guid? Id, string Name, IssueStatus Category, int? WipLimit, bool IsDefault);

/// <summary>An allowed move between two statuses, by their positions in the status list.</summary>
public sealed record WorkflowTransitionInput(int From, int To, bool RequireAssignee, TransitionActor AllowedBy);

/// <summary>Where the issues of a removed status go: the position of a status in the new list.</summary>
public sealed record WorkflowReplacementInput(Guid RemovedStatusId, int ReplacementIndex);

/// <summary>
/// Replaces a project's workflow as the editor submits it: every status in board order, whether
/// transitions are restricted, the allowed transitions, and where the issues of removed statuses
/// go. Project admins only.
/// </summary>
public sealed record UpdateWorkflowCommand(
    Guid ProjectId,
    WorkflowStatusInput[] Statuses,
    bool RestrictTransitions,
    WorkflowTransitionInput[] Transitions,
    WorkflowReplacementInput[]? Replacements = null) : ICommand<Result<WorkflowResponse>>;

public sealed class UpdateWorkflowCommandValidator : AbstractValidator<UpdateWorkflowCommand>
{
    public UpdateWorkflowCommandValidator()
    {
        RuleFor(command => command.Statuses).NotEmpty().Must(statuses => statuses.Length <= Workflow.MaxStatuses)
            .WithMessage($"A workflow has 1 to {Workflow.MaxStatuses} statuses.");
        RuleForEach(command => command.Statuses).ChildRules(status =>
        {
            status.RuleFor(input => input.Name).Must(name => name.Trim().Length is >= 1 and <= WorkflowStatus.MaxNameLength)
                .WithMessage($"Status names must be 1 to {WorkflowStatus.MaxNameLength} characters.");
            status.RuleFor(input => input.Category).IsInEnum();
            status.RuleFor(input => input.WipLimit).InclusiveBetween(1, WorkflowStatus.MaxWipLimit);
        });
        RuleForEach(command => command.Transitions).ChildRules(transition => transition.RuleFor(input => input.AllowedBy).IsInEnum());
        RuleFor(command => command.Transitions).Must(transitions => transitions.Length <= Workflow.MaxStatuses * Workflow.MaxStatuses)
            .WithMessage("Too many transitions.");
    }
}

public sealed class UpdateWorkflowCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    IWorkflowCache workflows,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateWorkflowCommand, Result<WorkflowResponse>>
{
    public async ValueTask<Result<WorkflowResponse>> Handle(UpdateWorkflowCommand command, CancellationToken cancellationToken)
    {
        var role = await gate.RoleInAsync(command.ProjectId, cancellationToken);
        if (!role.CanRead())
        {
            return ProjectErrors.NotFound;
        }

        if (!role.CanManage())
        {
            return ProjectErrors.Forbidden;
        }

        if (await db.Projects.AnyAsync(project => project.Id == command.ProjectId && project.ArchivedAt != null, cancellationToken))
        {
            return ProjectErrors.Archived;
        }

        var workflow = await db.Workflows
            .Include("_statuses")
            .Include("_transitions")
            .SingleOrDefaultAsync(candidate => candidate.ProjectId == command.ProjectId, cancellationToken);
        if (workflow is null)
        {
            return ProjectErrors.NotFound;
        }

        var before = workflow.Statuses.ToDictionary(status => status.Id, status => status.Category);
        var redefined = workflow.Redefine(
            [.. command.Statuses.Select(input => new StatusDraft(input.Id, input.Name, input.Category, input.WipLimit, input.IsDefault))],
            command.RestrictTransitions,
            [.. command.Transitions.Select(input => new TransitionDraft(input.From, input.To, input.RequireAssignee, input.AllowedBy))]);
        if (redefined.IsFailure)
        {
            return redefined.Error;
        }

        var changes = redefined.Value;
        var statuses = workflow.Statuses;

        // Every removed status that still has issues needs somewhere to send them.
        var occupied = changes.RemovedStatusIds.Count == 0
            ? []
            : await db.Issues
                .Where(issue => issue.ProjectId == command.ProjectId && changes.RemovedStatusIds.Contains(issue.StatusId))
                .Select(issue => issue.StatusId)
                .Distinct()
                .ToListAsync(cancellationToken);
        var replacements = new Dictionary<Guid, WorkflowStatus>();
        foreach (var removedId in occupied)
        {
            var input = command.Replacements?.FirstOrDefault(replacement => replacement.RemovedStatusId == removedId);
            if (input is null || input.ReplacementIndex < 0 || input.ReplacementIndex >= statuses.Count)
            {
                return WorkflowErrors.ReplacementRequired;
            }

            replacements[removedId] = statuses[input.ReplacementIndex];
        }

        var now = timeProvider.GetUtcNow();
        var countsChanged = changes.RecategorizedStatusIds.Any(id => before[id].IsClosed() != workflow.FindStatus(id)!.Category.IsClosed())
            || replacements.Any(pair => before[pair.Key].IsClosed() != pair.Value.Category.IsClosed());

        // The foreign key from issues to statuses is checked at commit, so the removed statuses can
        // be deleted before their issues move in the same transaction.
        await using (var transaction = await db.BeginTransactionAsync(cancellationToken))
        {
            await db.SaveChangesAsync(cancellationToken);

            foreach (var (removedId, replacement) in replacements)
            {
                await MoveIssuesAsync(command.ProjectId, removedId, replacement, now, cancellationToken);
            }

            foreach (var statusId in changes.RecategorizedStatusIds)
            {
                await MoveIssuesAsync(command.ProjectId, statusId, workflow.FindStatus(statusId)!, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await workflows.InvalidateAsync(command.ProjectId, cancellationToken);
        if (countsChanged)
        {
            await db.RefreshOpenIssuesAsync(command.ProjectId, cancellationToken);
            await db.RefreshAllSubIssuesAsync(command.ProjectId, cancellationToken);
        }

        return WorkflowResponse.From(workflow);
    }

    /// <summary>Puts every issue of a status into another (or the same, recategorized) status.</summary>
    private Task<int> MoveIssuesAsync(Guid projectId, Guid fromStatusId, WorkflowStatus to, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var closed = to.Category.IsClosed();
        return db.Issues
            .Where(issue => issue.ProjectId == projectId && issue.StatusId == fromStatusId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(issue => issue.StatusId, to.Id)
                    .SetProperty(issue => issue.Status, to.Category)
                    .SetProperty(issue => issue.CompletedAt, issue => closed ? issue.CompletedAt ?? now : null),
                cancellationToken);
    }
}
