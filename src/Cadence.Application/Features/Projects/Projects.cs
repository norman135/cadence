using System.Globalization;
using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Domain.Common;
using Cadence.Domain.Projects;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Projects;

/// <summary>A project as the current user sees it, including what they may do in it.</summary>
public sealed record ProjectResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    ProjectRole Role,
    int OpenIssueCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt);

/// <summary>The projects the caller can see, by name. Archived projects are left out unless asked for.</summary>
public sealed record ListProjectsQuery(bool IncludeArchived) : IQuery<IReadOnlyList<ProjectResponse>>;

public sealed class ListProjectsQueryHandler(ICadenceDbContext db, ProjectGate gate, ICurrentUser currentUser)
    : IQueryHandler<ListProjectsQuery, IReadOnlyList<ProjectResponse>>
{
    public async ValueTask<IReadOnlyList<ProjectResponse>> Handle(ListProjectsQuery query, CancellationToken cancellationToken)
    {
        var visible = await gate.VisibleProjectIdsAsync(cancellationToken);
        var userId = currentUser.RequiredUserId;

        var projects = db.Projects.AsNoTracking();
        if (visible is not null)
        {
            projects = projects.Where(project => visible.Contains(project.Id));
        }

        if (!query.IncludeArchived)
        {
            projects = projects.Where(project => project.ArchivedAt == null);
        }

        var rows = await projects
            .OrderBy(project => project.Name)
            .Select(project => new
            {
                Project = project,
                MemberRole = db.ProjectMembers
                    .Where(member => member.ProjectId == project.Id && member.UserId == userId)
                    .Select(member => (ProjectRole?)member.Role)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // Organization owners and admins see every project as admins.
        return [.. rows.Select(row => row.Project.ToResponse(visible is null ? ProjectRole.Admin : row.MemberRole ?? ProjectRole.Viewer))];
    }
}

public sealed record GetProjectQuery(Guid ProjectId) : IQuery<Result<ProjectResponse>>;

public sealed class GetProjectQueryHandler(ICadenceDbContext db, ProjectGate gate)
    : IQueryHandler<GetProjectQuery, Result<ProjectResponse>>
{
    public async ValueTask<Result<ProjectResponse>> Handle(GetProjectQuery query, CancellationToken cancellationToken)
    {
        if (await gate.RoleInAsync(query.ProjectId, cancellationToken) is not { } role)
        {
            return ProjectErrors.NotFound;
        }

        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(project => project.Id == query.ProjectId, cancellationToken);
        return project is null ? ProjectErrors.NotFound : project.ToResponse(role);
    }
}

/// <summary>
/// Creates a project with the caller as its admin. Without a key, one is suggested from the name and
/// made unique (ATL, ATL2, ATL3...).
/// </summary>
public sealed record CreateProjectCommand(string Name, string? Key, string? Description) : ICommand<Result<ProjectResponse>>;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => name.Trim().Length is >= Project.MinNameLength and <= Project.MaxNameLength)
            .WithMessage($"Name must be {Project.MinNameLength} to {Project.MaxNameLength} characters.");
        RuleFor(command => command.Key)
            .Must(key => key is null || Project.IsValidKey(key.Trim().ToUpperInvariant()))
            .WithMessage("Keys are 2 to 10 uppercase letters or digits, starting with a letter.");
        RuleFor(command => command.Description).MaximumLength(Project.MaxDescriptionLength);
    }
}

public sealed class CreateProjectCommandHandler(
    ICadenceDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IProjectAccessCache access,
    TimeProvider timeProvider)
    : ICommandHandler<CreateProjectCommand, Result<ProjectResponse>>
{
    public async ValueTask<Result<ProjectResponse>> Handle(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequiredUserId;
        var now = timeProvider.GetUtcNow();

        string key;
        if (command.Key is { } requested)
        {
            key = requested.Trim().ToUpperInvariant();
            if (await db.Projects.AnyAsync(project => project.Key == key, cancellationToken))
            {
                return ProjectErrors.KeyTaken;
            }
        }
        else
        {
            key = await UniqueKeyAsync(Project.SuggestKey(command.Name), cancellationToken);
        }

        var created = Project.Create(tenant.OrganizationId, key, command.Name, command.Description, userId, now);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var project = created.Value;
        db.Projects.Add(project);
        db.ProjectMembers.Add(ProjectMember.Add(project, userId, ProjectRole.Admin, now));
        await db.SaveChangesAsync(cancellationToken);

        await access.InvalidateAsync(tenant.OrganizationId, project.Id, userId, cancellationToken);
        return project.ToResponse(ProjectRole.Admin);
    }

    private async Task<string> UniqueKeyAsync(string suggested, CancellationToken cancellationToken)
    {
        var taken = await db.Projects
            .Where(project => project.Key.StartsWith(suggested))
            .Select(project => project.Key)
            .ToListAsync(cancellationToken);

        var candidate = suggested;
        for (var suffix = 2; taken.Contains(candidate); suffix++)
        {
            var digits = suffix.ToString(CultureInfo.InvariantCulture);
            candidate = suggested[..Math.Min(suggested.Length, Project.MaxKeyLength - digits.Length)] + digits;
        }

        return candidate;
    }
}

/// <summary>Renames or re-describes a project. Fields left out are unchanged; an empty description clears it.</summary>
public sealed record UpdateProjectCommand(Guid ProjectId, string? Name, string? Description) : ICommand<Result<ProjectResponse>>;

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => name is null || name.Trim().Length is >= Project.MinNameLength and <= Project.MaxNameLength)
            .WithMessage($"Name must be {Project.MinNameLength} to {Project.MaxNameLength} characters.");
        RuleFor(command => command.Description).MaximumLength(Project.MaxDescriptionLength);
    }
}

public sealed class UpdateProjectCommandHandler(ICadenceDbContext db, ProjectGate gate)
    : ICommandHandler<UpdateProjectCommand, Result<ProjectResponse>>
{
    public async ValueTask<Result<ProjectResponse>> Handle(UpdateProjectCommand command, CancellationToken cancellationToken)
    {
        var loaded = await ManagedProject.LoadAsync(db, gate, command.ProjectId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var project = loaded.Value;
        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        var result = command.Name is { } name ? project.Rename(name) : Result.Success();
        if (result.IsSuccess && command.Description is not null)
        {
            result = project.Describe(command.Description);
        }

        if (result.IsFailure)
        {
            return result.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return project.ToResponse(ProjectRole.Admin);
    }
}

/// <summary>Archives a project: it stays readable but takes no changes and leaves navigation.</summary>
public sealed record ArchiveProjectCommand(Guid ProjectId) : ICommand<Result>;

public sealed class ArchiveProjectCommandHandler(ICadenceDbContext db, ProjectGate gate, TimeProvider timeProvider)
    : ICommandHandler<ArchiveProjectCommand, Result>
{
    public async ValueTask<Result> Handle(ArchiveProjectCommand command, CancellationToken cancellationToken)
    {
        var loaded = await ManagedProject.LoadAsync(db, gate, command.ProjectId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        loaded.Value.Archive(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record RestoreProjectCommand(Guid ProjectId) : ICommand<Result>;

public sealed class RestoreProjectCommandHandler(ICadenceDbContext db, ProjectGate gate)
    : ICommandHandler<RestoreProjectCommand, Result>
{
    public async ValueTask<Result> Handle(RestoreProjectCommand command, CancellationToken cancellationToken)
    {
        var loaded = await ManagedProject.LoadAsync(db, gate, command.ProjectId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        loaded.Value.Restore();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class ProjectMappings
{
    public static ProjectResponse ToResponse(this Project project, ProjectRole role) =>
        new(project.Id, project.Key, project.Name, project.Description, role, project.OpenIssueCount, project.CreatedAt, project.ArchivedAt);
}

/// <summary>Loads a project the caller may manage: not found for outsiders, forbidden for non-admins.</summary>
internal static class ManagedProject
{
    public static async Task<Result<Project>> LoadAsync(ICadenceDbContext db, ProjectGate gate, Guid projectId, CancellationToken cancellationToken)
    {
        var role = await gate.RoleInAsync(projectId, cancellationToken);
        if (!role.CanRead())
        {
            return ProjectErrors.NotFound;
        }

        if (!role.CanManage())
        {
            return ProjectErrors.Forbidden;
        }

        var project = await db.Projects.SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);
        return project is null ? ProjectErrors.NotFound : project;
    }
}
