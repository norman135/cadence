using Cadence.Domain.Organizations;

namespace Cadence.Domain.Projects;

/// <summary>
/// Who may do what in a project. Like organization permissions, handlers check capabilities, never
/// roles, so the rules live here and nowhere else.
/// </summary>
public static class ProjectAccess
{
    /// <summary>
    /// The role that applies to a user in a project: organization owners and admins manage every
    /// project; everyone else needs a project membership. <see langword="null"/> means no access.
    /// </summary>
    public static ProjectRole? EffectiveRole(OrganizationRole organizationRole, ProjectRole? projectRole) =>
        organizationRole is OrganizationRole.Owner or OrganizationRole.Admin ? ProjectRole.Admin : projectRole;

    public static bool CanRead(this ProjectRole? role) => role is not null;

    /// <summary>Create and edit issues, comment, and link issues.</summary>
    public static bool CanWrite(this ProjectRole? role) => role is ProjectRole.Member or ProjectRole.Admin;

    /// <summary>Edit and archive the project, and manage its members.</summary>
    public static bool CanManage(this ProjectRole? role) => role is ProjectRole.Admin;
}
