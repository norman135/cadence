using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using Cadence.Domain.Projects;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Issues;

/// <summary>
/// Links an issue to another, found by key (ATL-142), which may be in any project the caller can
/// see. "Blocks" and "duplicates" read from this issue towards the other one.
/// </summary>
public sealed record CreateIssueLinkCommand(Guid IssueId, string TargetKey, IssueLinkType Type) : ICommand<Result>;

public sealed class CreateIssueLinkCommandValidator : AbstractValidator<CreateIssueLinkCommand>
{
    public CreateIssueLinkCommandValidator()
    {
        RuleFor(command => command.TargetKey).NotEmpty();
        RuleFor(command => command.Type).IsInEnum();
    }
}

public sealed class CreateIssueLinkCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CreateIssueLinkCommand, Result>
{
    public async ValueTask<Result> Handle(CreateIssueLinkCommand command, CancellationToken cancellationToken)
    {
        var loaded = await WritableIssue.LoadAsync(db, gate, command.IssueId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        if (!IssueKeys.TryParse(command.TargetKey, out var projectKey, out var number))
        {
            return IssueErrors.NotFound;
        }

        var target = await (
            from issue in db.Issues.AsNoTracking()
            join project in db.Projects on issue.ProjectId equals project.Id
            where project.Key == projectKey && issue.Number == number
            select issue)
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null || !(await gate.RoleInAsync(target.ProjectId, cancellationToken)).CanRead())
        {
            return IssueErrors.NotFound;
        }

        var created = IssueLink.Create(loaded.Value.Issue, target, command.Type, currentUser.RequiredUserId, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        var link = created.Value;
        if (await db.IssueLinks.AnyAsync(
            existing => existing.SourceIssueId == link.SourceIssueId && existing.TargetIssueId == link.TargetIssueId && existing.Type == link.Type,
            cancellationToken))
        {
            return IssueErrors.LinkExists;
        }

        db.IssueLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Removes a link; either linked issue's page can do it.</summary>
public sealed record DeleteIssueLinkCommand(Guid IssueId, Guid LinkId) : ICommand<Result>;

public sealed class DeleteIssueLinkCommandHandler(ICadenceDbContext db, ProjectGate gate)
    : ICommandHandler<DeleteIssueLinkCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteIssueLinkCommand command, CancellationToken cancellationToken)
    {
        var loaded = await WritableIssue.LoadAsync(db, gate, command.IssueId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var deleted = await db.IssueLinks
            .Where(link => link.Id == command.LinkId && (link.SourceIssueId == command.IssueId || link.TargetIssueId == command.IssueId))
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? IssueErrors.LinkNotFound : Result.Success();
    }
}

/// <summary>Loads an issue the caller may read, for the comment endpoints.</summary>
internal static class ReadableIssue
{
    public static async Task<Result<(Issue Issue, ProjectRole Role)>> LoadAsync(
        ICadenceDbContext db,
        ProjectGate gate,
        Guid issueId,
        CancellationToken cancellationToken)
    {
        var issue = await db.Issues.AsNoTracking().SingleOrDefaultAsync(issue => issue.Id == issueId, cancellationToken);
        if (issue is null || await gate.RoleInAsync(issue.ProjectId, cancellationToken) is not { } role)
        {
            return IssueErrors.NotFound;
        }

        return (issue, role);
    }
}
