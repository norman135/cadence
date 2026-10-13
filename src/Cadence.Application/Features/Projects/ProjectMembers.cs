using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Access;
using Cadence.Domain.Common;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Projects;

/// <summary>
/// Someone with access to a project. Organization owners and admins have access without being
/// members: they are listed with <paramref name="IsImplicit"/> set, and can't be removed here.
/// </summary>
public sealed record ProjectMemberResponse(Guid UserId, string DisplayName, string Email, ProjectRole Role, bool IsImplicit);

/// <summary>Everyone with access to the project, by name: its members and the organization's owners and admins.</summary>
public sealed record ListProjectMembersQuery(Guid ProjectId) : IQuery<Result<IReadOnlyList<ProjectMemberResponse>>>;

public sealed class ListProjectMembersQueryHandler(ICadenceDbContext db, ProjectGate gate)
    : IQueryHandler<ListProjectMembersQuery, Result<IReadOnlyList<ProjectMemberResponse>>>
{
    public async ValueTask<Result<IReadOnlyList<ProjectMemberResponse>>> Handle(ListProjectMembersQuery query, CancellationToken cancellationToken)
    {
        if (!(await gate.RoleInAsync(query.ProjectId, cancellationToken)).CanRead())
        {
            return ProjectErrors.NotFound;
        }

        var rows = await ProjectPeople.WithAccess(db, query.ProjectId).ToListAsync(cancellationToken);

        return rows
            .OrderBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new ProjectMemberResponse(
                row.UserId,
                row.DisplayName,
                row.Email,
                row.OrganizationWide ? ProjectRole.Admin : row.ProjectRole!.Value,
                row.OrganizationWide && row.ProjectRole is null))
            .ToList();
    }
}

/// <summary>Gives an organization member access to the project.</summary>
public sealed record AddProjectMemberCommand(Guid ProjectId, Guid UserId, ProjectRole Role) : ICommand<Result>;

public sealed class AddProjectMemberCommandValidator : AbstractValidator<AddProjectMemberCommand>
{
    public AddProjectMemberCommandValidator() => RuleFor(command => command.Role).IsInEnum();
}

public sealed class AddProjectMemberCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ITenantContext tenant,
    IProjectAccessCache access,
    TimeProvider timeProvider)
    : ICommandHandler<AddProjectMemberCommand, Result>
{
    public async ValueTask<Result> Handle(AddProjectMemberCommand command, CancellationToken cancellationToken)
    {
        var loaded = await ManagedProject.LoadAsync(db, gate, command.ProjectId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        if (!await db.Memberships.AnyAsync(membership => membership.UserId == command.UserId, cancellationToken))
        {
            return ProjectErrors.NotInOrganization;
        }

        if (await db.ProjectMembers.AnyAsync(member => member.ProjectId == command.ProjectId && member.UserId == command.UserId, cancellationToken))
        {
            return ProjectErrors.AlreadyMember;
        }

        db.ProjectMembers.Add(ProjectMember.Add(loaded.Value, command.UserId, command.Role, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);

        await access.InvalidateAsync(tenant.OrganizationId, command.ProjectId, command.UserId, cancellationToken);
        return Result.Success();
    }
}

public sealed record ChangeProjectMemberRoleCommand(Guid ProjectId, Guid UserId, ProjectRole Role) : ICommand<Result>;

public sealed class ChangeProjectMemberRoleCommandValidator : AbstractValidator<ChangeProjectMemberRoleCommand>
{
    public ChangeProjectMemberRoleCommandValidator() => RuleFor(command => command.Role).IsInEnum();
}

public sealed class ChangeProjectMemberRoleCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ITenantContext tenant,
    IProjectAccessCache access)
    : ICommandHandler<ChangeProjectMemberRoleCommand, Result>
{
    public async ValueTask<Result> Handle(ChangeProjectMemberRoleCommand command, CancellationToken cancellationToken)
    {
        var loaded = await ManagedProject.LoadAsync(db, gate, command.ProjectId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var member = await db.ProjectMembers
            .SingleOrDefaultAsync(member => member.ProjectId == command.ProjectId && member.UserId == command.UserId, cancellationToken);
        if (member is null)
        {
            return ProjectErrors.MemberNotFound;
        }

        var admins = await db.ProjectMembers
            .CountAsync(other => other.ProjectId == command.ProjectId && other.Role == ProjectRole.Admin, cancellationToken);
        var changed = member.ChangeRole(command.Role, admins);
        if (changed.IsFailure)
        {
            return changed;
        }

        await db.SaveChangesAsync(cancellationToken);
        await access.InvalidateAsync(tenant.OrganizationId, command.ProjectId, command.UserId, cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Removes someone from the project. Members may always leave; removing someone else takes the admin
/// role. The last admin can do neither.
/// </summary>
public sealed record RemoveProjectMemberCommand(Guid ProjectId, Guid UserId) : ICommand<Result>;

public sealed class RemoveProjectMemberCommandHandler(
    ICadenceDbContext db,
    ProjectGate gate,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IProjectAccessCache access)
    : ICommandHandler<RemoveProjectMemberCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveProjectMemberCommand command, CancellationToken cancellationToken)
    {
        var role = await gate.RoleInAsync(command.ProjectId, cancellationToken);
        if (!role.CanRead())
        {
            return ProjectErrors.NotFound;
        }

        var leaving = command.UserId == currentUser.RequiredUserId;
        if (!leaving && !role.CanManage())
        {
            return ProjectErrors.Forbidden;
        }

        var member = await db.ProjectMembers
            .SingleOrDefaultAsync(member => member.ProjectId == command.ProjectId && member.UserId == command.UserId, cancellationToken);
        if (member is null)
        {
            return ProjectErrors.MemberNotFound;
        }

        var admins = await db.ProjectMembers
            .CountAsync(other => other.ProjectId == command.ProjectId && other.Role == ProjectRole.Admin, cancellationToken);
        var removable = member.CanBeRemoved(admins);
        if (removable.IsFailure)
        {
            return removable;
        }

        db.ProjectMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        await access.InvalidateAsync(tenant.OrganizationId, command.ProjectId, command.UserId, cancellationToken);
        return Result.Success();
    }
}

/// <summary>Who has access to a project, as one query.</summary>
internal static class ProjectPeople
{
    public static IQueryable<PersonWithAccess> WithAccess(ICadenceDbContext db, Guid projectId) =>
        from membership in db.Memberships.AsNoTracking()
        join user in db.Users on membership.UserId equals user.Id
        let projectRole = db.ProjectMembers
            .Where(member => member.ProjectId == projectId && member.UserId == membership.UserId)
            .Select(member => (ProjectRole?)member.Role)
            .FirstOrDefault()
        let organizationWide = membership.Role == OrganizationRole.Owner || membership.Role == OrganizationRole.Admin
        where organizationWide || projectRole != null
        select new PersonWithAccess
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            OrganizationWide = organizationWide,
            ProjectRole = projectRole,
        };

    /// <summary>Whether a user may be assigned issues in the project (they have access to it).</summary>
    public static Task<bool> HasAccessAsync(ICadenceDbContext db, Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(
            membership => membership.UserId == userId
                && (membership.Role == OrganizationRole.Owner
                    || membership.Role == OrganizationRole.Admin
                    || db.ProjectMembers.Any(member => member.ProjectId == projectId && member.UserId == userId)),
            cancellationToken);

    /// <summary>A row of <see cref="WithAccess"/>, before it becomes a response.</summary>
    internal sealed class PersonWithAccess
    {
        public Guid UserId { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public bool OrganizationWide { get; init; }

        public ProjectRole? ProjectRole { get; init; }
    }
}
