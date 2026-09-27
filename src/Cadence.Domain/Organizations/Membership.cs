using Cadence.Domain.Common;

namespace Cadence.Domain.Organizations;

/// <summary>
/// A user's membership of an organization. Kept as its own entity rather than a collection on
/// <see cref="Organization"/>, so adding one member never loads every member.
/// </summary>
public sealed class Membership : Entity<Guid>, ITenantScoped
{
    private Membership(Guid id, Guid organizationId, Guid userId, OrganizationRole role, DateTimeOffset joinedAt)
        : base(id)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    /// <summary>Used by EF Core.</summary>
    private Membership()
    {
    }

    public Guid OrganizationId { get; private init; }

    public Guid UserId { get; private init; }

    public OrganizationRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private init; }

    public static Membership Create(Guid organizationId, Guid userId, OrganizationRole role, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), organizationId, userId, role, now);

    /// <summary>
    /// Changes the role according to the ownership rules: only owners may grant the owner role or
    /// change an owner's role, and the last owner cannot step down.
    /// </summary>
    /// <param name="actorRole">The role of the member making the change.</param>
    /// <param name="newRole">The role to assign.</param>
    /// <param name="ownerCount">How many owners the organization has now.</param>
    public Result ChangeRole(OrganizationRole actorRole, OrganizationRole newRole, int ownerCount)
    {
        var touchesOwnership = Role == OrganizationRole.Owner || newRole == OrganizationRole.Owner;
        if (touchesOwnership && actorRole != OrganizationRole.Owner)
        {
            return OrganizationErrors.OwnerRequired;
        }

        if (Role == OrganizationRole.Owner && newRole != OrganizationRole.Owner && ownerCount <= 1)
        {
            return OrganizationErrors.LastOwner;
        }

        Role = newRole;
        return Result.Success();
    }

    /// <summary>Whether this membership may be ended, by an administrator or by the member leaving.</summary>
    /// <param name="actorRole">The role of the member removing this one (their own role when leaving).</param>
    /// <param name="ownerCount">How many owners the organization has now.</param>
    public Result CanBeRemoved(OrganizationRole actorRole, int ownerCount)
    {
        if (Role == OrganizationRole.Owner && actorRole != OrganizationRole.Owner)
        {
            return OrganizationErrors.OwnerRequired;
        }

        return Role == OrganizationRole.Owner && ownerCount <= 1
            ? OrganizationErrors.LastOwner
            : Result.Success();
    }
}
