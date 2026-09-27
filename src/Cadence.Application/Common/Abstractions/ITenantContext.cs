using System.ComponentModel;
using Cadence.Domain.Organizations;

namespace Cadence.Application.Common.Abstractions;

/// <summary>
/// The organization a request operates on and the caller's role in it. Resolved once per request from
/// the cached membership, so permission checks cost no database queries.
/// </summary>
public interface ITenantContext
{
    /// <summary>Whether the request targets an organization the caller belongs to.</summary>
    bool IsResolved { get; }

    /// <exception cref="InvalidOperationException">No organization was resolved for the request.</exception>
    Guid OrganizationId { get; }

    /// <exception cref="InvalidOperationException">No organization was resolved for the request.</exception>
    OrganizationRole Role { get; }

    bool HasPermission(string permission) => IsResolved && Role.Grants(permission);
}

/// <summary>
/// A user's membership as cached for permission checks. Immutable, so the in-process cache can hand
/// out the same instance instead of deserializing a copy on every hit.
/// </summary>
[ImmutableObject(true)]
public sealed record MembershipSnapshot(Guid OrganizationId, Guid UserId, OrganizationRole Role);

/// <summary>Cached membership lookups, invalidated whenever a membership changes.</summary>
public interface IMembershipCache
{
    /// <summary>The user's membership, or <see langword="null"/> when they don't belong to the organization.</summary>
    ValueTask<MembershipSnapshot?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    ValueTask InvalidateAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    ValueTask InvalidateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
}
