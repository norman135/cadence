using System.Collections.Frozen;

namespace Cadence.Domain.Organizations;

/// <summary>
/// Everything a member can be allowed to do in an organization. Endpoints and handlers check
/// permissions, never roles, so what each role may do is defined in exactly one place.
/// </summary>
public static class Permissions
{
    public const string OrganizationUpdate = "organization.update";
    public const string OrganizationDelete = "organization.delete";
    public const string MembersRead = "members.read";
    public const string MembersInvite = "members.invite";
    public const string MembersManage = "members.manage";

    /// <summary>Create projects (the creator becomes the project's admin).</summary>
    public const string ProjectsCreate = "projects.create";

    /// <summary>Create, rename and delete the organization's issue labels.</summary>
    public const string LabelsManage = "labels.manage";

    private static readonly FrozenDictionary<OrganizationRole, FrozenSet<string>> s_byRole =
        new Dictionary<OrganizationRole, FrozenSet<string>>
        {
            [OrganizationRole.Guest] = FrozenSet<string>.Empty,
            [OrganizationRole.Member] = new[] { MembersRead, ProjectsCreate, LabelsManage }.ToFrozenSet(),
            [OrganizationRole.Admin] = new[] { OrganizationUpdate, MembersRead, MembersInvite, MembersManage, ProjectsCreate, LabelsManage }.ToFrozenSet(),
            [OrganizationRole.Owner] = new[] { OrganizationUpdate, OrganizationDelete, MembersRead, MembersInvite, MembersManage, ProjectsCreate, LabelsManage }.ToFrozenSet(),
        }.ToFrozenDictionary();

    /// <summary>The permissions granted to a role.</summary>
    public static FrozenSet<string> For(OrganizationRole role) =>
        s_byRole.TryGetValue(role, out var permissions) ? permissions : FrozenSet<string>.Empty;

    public static bool Grants(this OrganizationRole role, string permission) => For(role).Contains(permission);
}
