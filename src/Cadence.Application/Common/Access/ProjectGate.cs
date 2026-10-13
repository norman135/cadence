using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Projects;

namespace Cadence.Application.Common.Access;

/// <summary>
/// Answers "what may the current user do in this project?" for the request's organization, from the
/// tenant context and the project access cache. Organization owners and admins manage every project
/// (<see cref="ProjectAccess.EffectiveRole"/>).
/// </summary>
public sealed class ProjectGate(ITenantContext tenant, ICurrentUser currentUser, IProjectAccessCache cache)
{
    /// <summary>The user's effective role in a project of the current organization; <see langword="null"/> means no access.</summary>
    public async ValueTask<ProjectRole?> RoleInAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // Owners and admins need no lookup; everyone else needs a project membership.
        if (ProjectAccess.EffectiveRole(tenant.Role, projectRole: null) is { } organizationWide)
        {
            return organizationWide;
        }

        var membership = await cache.GetRoleAsync(tenant.OrganizationId, projectId, currentUser.RequiredUserId, cancellationToken);
        return ProjectAccess.EffectiveRole(tenant.Role, membership);
    }

    /// <summary>
    /// The projects the user can see, for filtering lists: <see langword="null"/> means every project
    /// in the organization (owners and admins).
    /// </summary>
    public async ValueTask<IReadOnlyList<Guid>?> VisibleProjectIdsAsync(CancellationToken cancellationToken) =>
        ProjectAccess.EffectiveRole(tenant.Role, projectRole: null) is not null
            ? null
            : await cache.GetProjectIdsAsync(tenant.OrganizationId, currentUser.RequiredUserId, cancellationToken);
}
