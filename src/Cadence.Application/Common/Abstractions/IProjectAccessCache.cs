using Cadence.Domain.Projects;

namespace Cadence.Application.Common.Abstractions;

/// <summary>
/// Cached project memberships. Every issue request checks project access, so a hit keeps that
/// check free of database queries. Invalidated whenever a project membership changes.
/// </summary>
public interface IProjectAccessCache
{
    /// <summary>The user's role in the project, or <see langword="null"/> when they are not a member.</summary>
    ValueTask<ProjectRole?> GetRoleAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken);

    /// <summary>The projects the user is a member of in the organization.</summary>
    ValueTask<IReadOnlyList<Guid>> GetProjectIdsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Forgets one user's access to one project, and their project list.</summary>
    ValueTask InvalidateAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Forgets every cached project membership of a user in the organization.</summary>
    ValueTask InvalidateUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
}

/// <summary>Hands out issue numbers. Safe under concurrent creates in the same project.</summary>
public interface IIssueNumbers
{
    /// <summary>The next number in the project. A number is never handed out twice, even if the issue isn't saved.</summary>
    Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken);
}
