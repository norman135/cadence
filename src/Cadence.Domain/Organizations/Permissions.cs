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

    private static readonly FrozenDictionary<OrganizationRole, FrozenSet<string>> s_byRole =
        new Dictionary<OrganizationRole, FrozenSet<string>>
        {
            [OrganizationRole.Guest] = FrozenSet<string>.Empty,
            [OrganizationRole.Member] = new[] { MembersRead }.ToFrozenSet(),
            [OrganizationRole.Admin] = new[] { OrganizationUpdate, MembersRead, MembersInvite, MembersManage }.ToFrozenSet(),
            [OrganizationRole.Owner] = new[] { OrganizationUpdate, OrganizationDelete, MembersRead, MembersInvite, MembersManage }.ToFrozenSet(),
        }.ToFrozenDictionary();

    /// <summary>The permissions granted to a role.</summary>
    public static FrozenSet<string> For(OrganizationRole role) =>
        s_byRole.TryGetValue(role, out var permissions) ? permissions : FrozenSet<string>.Empty;

    public static bool Grants(this OrganizationRole role, string permission) => For(role).Contains(permission);
}
