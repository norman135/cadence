using Cadence.Application.Common.ReadModels;
using Cadence.Domain.Issues;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    /// <summary>Filtered to the current organization. Check project access before returning anything.</summary>
    DbSet<Project> Projects { get; }

    /// <summary>Filtered to the current organization.</summary>
    DbSet<ProjectMember> ProjectMembers { get; }

    /// <summary>Filtered to the current organization. Check project access before returning anything.</summary>
    DbSet<Issue> Issues { get; }

    /// <summary>Filtered to the current organization.</summary>
    DbSet<Label> Labels { get; }

    /// <summary>Filtered to the current organization.</summary>
    DbSet<IssueLabel> IssueLabels { get; }

    /// <summary>Filtered to the current organization.</summary>
    DbSet<IssueLink> IssueLinks { get; }

    /// <summary>Filtered to the current organization.</summary>
    DbSet<Comment> Comments { get; }

    /// <summary>Filtered to the current organization.</summary>
    DbSet<CommentRevision> CommentRevisions { get; }

    /// <summary>Filtered to the current organization. Load with Include for the statuses and transitions.</summary>
    DbSet<Workflow> Workflows { get; }

    /// <summary>Filtered to the current organization; read-only use (the workflow saves them).</summary>
    DbSet<WorkflowStatus> WorkflowStatuses { get; }

    /// <summary>Read-only view of user accounts, for joining names and emails into responses.</summary>
    IQueryable<UserSummary> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts a transaction for changes that must land together (several statements).</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

/// <summary>Names of the global query filters, for the rare queries that must look across tenants.</summary>
public static class QueryFilters
{
    /// <summary>Restricts tenant-scoped entities to the organization of the current request.</summary>
    public const string Tenant = "Tenant";
}
