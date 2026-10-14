using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Application.Features.Projects;
using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Issues;

/// <summary>Fields an update can clear (set to empty), since leaving a field out means "unchanged".</summary>
public enum IssueField
{
    Description,
    Assignee,
    Estimate,
    DueDate,
    Parent,
}

public sealed record CreatedIssueResponse(Guid Id, string Key);

/// <summary>Creates an issue in a project. Only the title is required; the rest has sensible defaults.</summary>
public sealed record CreateIssueCommand(
    Guid ProjectId,
    string Title,
    string? Description = null,
    IssueType? Type = null,
    Guid? StatusId = null,
    IssueStatus? Status = null,
    IssuePriority? Priority = null,
    Guid? AssigneeId = null,
    Guid[]? LabelIds = null,
    int? Estimate = null,
    DateOnly? DueDate = null,
    Guid? ParentId = null) : ICommand<Result<CreatedIssueResponse>>;

public sealed class CreateIssueCommandValidator : AbstractValidator<CreateIssueCommand>
{
    public CreateIssueCommandValidator()
    {
        RuleFor(command => command.Title).Must(title => title.Trim().Length is >= 1 and <= Issue.MaxTitleLength)
            .WithMessage($"Title must be 1 to {Issue.MaxTitleLength} characters.");
        RuleFor(command => command.Description).MaximumLength(Issue.MaxDescriptionLength);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Priority).IsInEnum();
        RuleFor(command => command.Estimate).InclusiveBetween(0, Issue.MaxEstimate);
        RuleFor(command => command.LabelIds).Must(ids => ids is null || ids.Distinct().Count() <= Issue.MaxLabels)
            .WithMessage($"An issue can have at most {Issue.MaxLabels} labels.");
    }
}

/// <summary>
/// Creates an issue in the project's default status (or the one asked for: creating isn't a
/// transition), at the end of the project's order.
/// </summary>
public sealed class CreateIssueCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IIssueNumbers numbers,
    IWorkflowCache workflows,
    TimeProvider timeProvider)
    : ICommandHandler<CreateIssueCommand, Result<CreatedIssueResponse>>
{
    public async ValueTask<Result<CreatedIssueResponse>> Handle(CreateIssueCommand command, CancellationToken cancellationToken)
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

        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(project => project.Id == command.ProjectId, cancellationToken);
        if (project is null)
        {
            return ProjectErrors.NotFound;
        }

        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        var checks = await IssueChanges.CheckReferencesAsync(db, project.Id, command.AssigneeId, command.LabelIds, cancellationToken);
        if (checks.IsFailure)
        {
            return checks.Error;
        }

        Issue? parent = null;
        List<Guid> ancestry = [];
        if (command.ParentId is { } parentId)
        {
            parent = await db.Issues.AsNoTracking().SingleOrDefaultAsync(issue => issue.Id == parentId, cancellationToken);
            if (parent is null)
            {
                return IssueErrors.ParentNotFound;
            }

            ancestry = await IssueTree.AncestryAsync(db, parent, cancellationToken);
        }

        var workflow = (await workflows.GetAsync(tenant.OrganizationId, project.Id, cancellationToken))?.ToDomain(tenant.OrganizationId);
        var status = IssueChanges.ResolveStatus(workflow, command.StatusId, command.Status) ?? workflow?.DefaultStatus;
        if (status is null)
        {
            return WorkflowErrors.UnknownStatus;
        }

        // New issues go to the end of the project's order. Two concurrent creates may get the same
        // key; ties order by id, and the next move between them separates them.
        var last = await db.Issues.Where(issue => issue.ProjectId == project.Id).MaxAsync(issue => (string?)issue.Rank, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var number = await numbers.NextAsync(project.Id, cancellationToken);
        var created = Issue.Create(
            project,
            number,
            command.Type ?? IssueType.Task,
            command.Title,
            command.Description,
            status,
            Rank.Between(last, null),
            command.Priority ?? IssuePriority.None,
            currentUser.RequiredUserId,
            now);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var issue = created.Value;
        Result[] applied =
        [
            issue.AssignTo(command.AssigneeId, now),
            issue.EstimateAt(command.Estimate, now),
            issue.DueOn(command.DueDate, now),
            parent is null ? Result.Success() : issue.MoveUnder(parent, ancestry, subtreeHeight: 0, now),
        ];
        if (applied.FirstOrDefault(result => result.IsFailure) is { } failure)
        {
            return failure.Error;
        }

        db.Issues.Add(issue);
        foreach (var labelId in (command.LabelIds ?? []).Distinct())
        {
            db.IssueLabels.Add(new IssueLabel(issue.OrganizationId, issue.Id, labelId));
        }

        await db.SaveChangesAsync(cancellationToken);
        await db.RefreshSubIssuesAsync(issue.ParentId, cancellationToken);
        await db.RefreshOpenIssuesAsync(project.Id, cancellationToken);

        return new CreatedIssueResponse(issue.Id, IssueKeys.Format(project.Key, number));
    }
}

/// <summary>
/// Changes some fields of an issue. Fields left out (null) are unchanged; list a field in
/// <paramref name="Clear"/> to empty it. <paramref name="LabelIds"/>, when given, replaces the labels.
/// </summary>
public sealed record UpdateIssueCommand(
    Guid IssueId,
    string? Title = null,
    string? Description = null,
    IssueType? Type = null,
    Guid? StatusId = null,
    IssueStatus? Status = null,
    IssuePriority? Priority = null,
    Guid? AssigneeId = null,
    int? Estimate = null,
    DateOnly? DueDate = null,
    Guid? ParentId = null,
    Guid[]? LabelIds = null,
    IssueField[]? Clear = null) : ICommand<Result>;

public sealed class UpdateIssueCommandValidator : AbstractValidator<UpdateIssueCommand>
{
    public UpdateIssueCommandValidator()
    {
        RuleFor(command => command.Title).Must(title => title is null || title.Trim().Length is >= 1 and <= Issue.MaxTitleLength)
            .WithMessage($"Title must be 1 to {Issue.MaxTitleLength} characters.");
        RuleFor(command => command.Description).MaximumLength(Issue.MaxDescriptionLength);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Priority).IsInEnum();
        RuleFor(command => command.Estimate).InclusiveBetween(0, Issue.MaxEstimate);
        RuleFor(command => command.LabelIds).Must(ids => ids is null || ids.Distinct().Count() <= Issue.MaxLabels)
            .WithMessage($"An issue can have at most {Issue.MaxLabels} labels.");
        RuleForEach(command => command.Clear).IsInEnum();
    }
}

public sealed class UpdateIssueCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IWorkflowCache workflows,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateIssueCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateIssueCommand command, CancellationToken cancellationToken)
    {
        var loaded = await WritableIssue.LoadAsync(db, gate, command.IssueId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var (issue, role) = loaded.Value;
        var clear = command.Clear?.ToHashSet() ?? [];
        var now = timeProvider.GetUtcNow();
        var previousParent = issue.ParentId;
        var wasClosed = issue.Status.IsClosed();

        var checks = await IssueChanges.CheckReferencesAsync(db, issue.ProjectId, command.AssigneeId, command.LabelIds, cancellationToken);
        if (checks.IsFailure)
        {
            return checks;
        }

        var steps = new List<Func<Task<Result>>>();
        if (command.Title is { } title)
        {
            steps.Add(() => Task.FromResult(issue.Retitle(title, now)));
        }

        if (command.Description is not null || clear.Contains(IssueField.Description))
        {
            steps.Add(() => Task.FromResult(issue.Describe(clear.Contains(IssueField.Description) ? null : command.Description, now)));
        }

        if (command.Type is { } type)
        {
            steps.Add(() => Task.FromResult(issue.ChangeType(type, now)));
        }

        if (command.Priority is { } priority)
        {
            steps.Add(() => Task.FromResult(issue.Prioritize(priority, now)));
        }

        if (command.AssigneeId is not null || clear.Contains(IssueField.Assignee))
        {
            steps.Add(() => Task.FromResult(issue.AssignTo(clear.Contains(IssueField.Assignee) ? null : command.AssigneeId, now)));
        }

        if (command.Estimate is not null || clear.Contains(IssueField.Estimate))
        {
            steps.Add(() => Task.FromResult(issue.EstimateAt(clear.Contains(IssueField.Estimate) ? null : command.Estimate, now)));
        }

        if (command.DueDate is not null || clear.Contains(IssueField.DueDate))
        {
            steps.Add(() => Task.FromResult(issue.DueOn(clear.Contains(IssueField.DueDate) ? null : command.DueDate, now)));
        }

        // After the assignee, so the transition rules see the assignee this update sets.
        if (command.StatusId is not null || command.Status is not null)
        {
            steps.Add(async () =>
            {
                var workflow = (await workflows.GetAsync(tenant.OrganizationId, issue.ProjectId, cancellationToken))?.ToDomain(tenant.OrganizationId);
                if (IssueChanges.ResolveStatus(workflow, command.StatusId, command.Status) is not { } status)
                {
                    return WorkflowErrors.UnknownStatus;
                }

                if (status.Id == issue.StatusId)
                {
                    return Result.Success();
                }

                var allowed = workflow!.CanMove(issue.StatusId, status.Id, issue.AssigneeId, currentUser.RequiredUserId, role);
                return allowed.IsFailure ? allowed : issue.MoveTo(status, now);
            });
        }

        if (clear.Contains(IssueField.Parent))
        {
            steps.Add(() => Task.FromResult(issue.MoveUnder(null, [], 0, now)));
        }
        else if (command.ParentId is { } parentId)
        {
            steps.Add(async () =>
            {
                var parent = await db.Issues.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == parentId, cancellationToken);
                if (parent is null)
                {
                    return IssueErrors.ParentNotFound;
                }

                var ancestry = await IssueTree.AncestryAsync(db, parent, cancellationToken);
                var height = await IssueTree.SubtreeHeightAsync(db, issue.Id, cancellationToken);
                return issue.MoveUnder(parent, ancestry, height, now);
            });
        }

        foreach (var step in steps)
        {
            var result = await step();
            if (result.IsFailure)
            {
                return result;
            }
        }

        if (command.LabelIds is { } labelIds)
        {
            await db.IssueLabels.Where(issueLabel => issueLabel.IssueId == issue.Id).ExecuteDeleteAsync(cancellationToken);
            foreach (var labelId in labelIds.Distinct())
            {
                db.IssueLabels.Add(new IssueLabel(issue.OrganizationId, issue.Id, labelId));
            }

            issue.LabelsChanged(now);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (previousParent != issue.ParentId)
        {
            await db.RefreshSubIssuesAsync(previousParent, cancellationToken);
            await db.RefreshSubIssuesAsync(issue.ParentId, cancellationToken);
        }
        else if (wasClosed != issue.Status.IsClosed())
        {
            await db.RefreshSubIssuesAsync(issue.ParentId, cancellationToken);
        }

        if (wasClosed != issue.Status.IsClosed())
        {
            await db.RefreshOpenIssuesAsync(issue.ProjectId, cancellationToken);
        }

        return Result.Success();
    }
}

/// <summary>Deletes an issue. Its sub-issues move to the top level; comments and links go with it.</summary>
public sealed record DeleteIssueCommand(Guid IssueId) : ICommand<Result>;

public sealed class DeleteIssueCommandHandler(ICadenceDbContext db, ProjectGate gate, ICurrentUser currentUser)
    : ICommandHandler<DeleteIssueCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteIssueCommand command, CancellationToken cancellationToken)
    {
        var loaded = await WritableIssue.LoadAsync(db, gate, command.IssueId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        // Members may delete what they reported; project admins may delete anything.
        var (issue, role) = loaded.Value;
        if (role != ProjectRole.Admin && issue.ReporterId != currentUser.RequiredUserId)
        {
            return ProjectErrors.Forbidden;
        }

        db.Issues.Remove(issue);
        await db.SaveChangesAsync(cancellationToken);
        await db.RefreshSubIssuesAsync(issue.ParentId, cancellationToken);
        await db.RefreshOpenIssuesAsync(issue.ProjectId, cancellationToken);
        return Result.Success();
    }
}

internal static class IssueChanges
{
    /// <summary>
    /// The status a request names: by id, or by category (the first status of that category in the
    /// workflow, for clients that think in categories). Null when neither is given or it doesn't exist.
    /// </summary>
    public static WorkflowStatus? ResolveStatus(Workflow? workflow, Guid? statusId, IssueStatus? category) =>
        statusId is { } id ? workflow?.FindStatus(id)
        : category is { } named ? workflow?.FirstIn(named)
        : null;

    /// <summary>The assignee must have access to the project, and every label must exist.</summary>
    public static async Task<Result> CheckReferencesAsync(
        ICadenceDbContext db,
        Guid projectId,
        Guid? assigneeId,
        Guid[]? labelIds,
        CancellationToken cancellationToken)
    {
        if (assigneeId is { } assignee && !await ProjectPeople.HasAccessAsync(db, projectId, assignee, cancellationToken))
        {
            return IssueErrors.AssigneeNotInProject;
        }

        if (labelIds is { Length: > 0 })
        {
            var distinct = labelIds.Distinct().ToArray();
            var found = await db.Labels.CountAsync(label => distinct.Contains(label.Id), cancellationToken);
            if (found != distinct.Length)
            {
                return IssueErrors.LabelNotFound;
            }
        }

        return Result.Success();
    }
}
