using Cadence.Application.Common.ReadModels;
using Cadence.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Common.Abstractions;

/// <summary>
/// The persistence surface handlers work with. Reads project straight into DTOs with LINQ (ADR-0002).
/// Sets of tenant-scoped entities are automatically filtered to the current organization (ADR-0007).
/// </summary>
public interface ICadenceDbContext
{
    DbSet<Organization> Organizations { get; }

    /// <summary>Filtered to the current organization.</summary>
    DbSet<Membership> Memberships { get; }

    /// <summary>Filtered to the current organization.</summary>
    DbSet<Invitation> Invitations { get; }

    /// <summary>Read-only view of user accounts, for joining names and emails into responses.</summary>
    IQueryable<UserSummary> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Names of the global query filters, for the rare queries that must look across tenants.</summary>
public static class QueryFilters
{
    /// <summary>Restricts tenant-scoped entities to the organization of the current request.</summary>
    public const string Tenant = "Tenant";
}
