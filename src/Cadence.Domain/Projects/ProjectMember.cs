using Cadence.Domain.Common;

namespace Cadence.Domain.Projects;

/// <summary>What a member may do in one project. Organization owners and admins are implicitly admins of every project.</summary>
public enum ProjectRole
{
    /// <summary>Reads issues and comments.</summary>
    Viewer,

    /// <summary>Also creates and edits issues, and comments.</summary>
    Member,

    /// <summary>Also edits the project, manages its members and archives it.</summary>
    Admin,
}

/// <summary>A user's access to a project. Organization members see only the projects they belong to.</summary>
public sealed class ProjectMember : Entity<Guid>, ITenantScoped
{
    private ProjectMember(Guid id, Guid organizationId, Guid projectId, Guid userId, ProjectRole role, DateTimeOffset addedAt)
        : base(id)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
        UserId = userId;
        Role = role;
        AddedAt = addedAt;
    }

    /// <summary>Used by EF Core.</summary>
    private ProjectMember()
    {
    }

    public Guid OrganizationId { get; private init; }

    public Guid ProjectId { get; private init; }

    public Guid UserId { get; private init; }

    public ProjectRole Role { get; private set; }

    public DateTimeOffset AddedAt { get; private init; }

    public static ProjectMember Add(Project project, Guid userId, ProjectRole role, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ProjectMember(Guid.CreateVersion7(), project.OrganizationId, project.Id, userId, role, now);
    }

    /// <summary>Changes the role. A project always keeps at least one admin among its members.</summary>
    public Result ChangeRole(ProjectRole role, int adminCount)
    {
        if (Role == ProjectRole.Admin && role != ProjectRole.Admin && adminCount <= 1)
        {
            return ProjectErrors.LastAdmin;
        }

        Role = role;
        return Result.Success();
    }

    /// <summary>The last admin can't be removed, so someone can always manage the project.</summary>
    public Result CanBeRemoved(int adminCount) =>
        Role == ProjectRole.Admin && adminCount <= 1 ? ProjectErrors.LastAdmin : Result.Success();
}
