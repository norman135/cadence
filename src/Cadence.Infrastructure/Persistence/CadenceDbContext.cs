using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.ReadModels;
using Cadence.Domain.Common;
using Cadence.Domain.Organizations;
using Cadence.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work for Cadence, including the ASP.NET Core Identity user store.
/// Entity mappings live in <c>IEntityTypeConfiguration</c> classes in this assembly and are applied automatically.
/// </summary>
/// <remarks>
/// Tenant isolation (ADR-0007): every <see cref="ITenantScoped"/> entity gets a query filter that
/// restricts it to the current organization, and saving one into any other organization throws.
/// Without a resolved organization, the filter matches nothing, so a missing tenant fails closed.
/// </remarks>
public sealed class CadenceDbContext(DbContextOptions<CadenceDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), ICadenceDbContext
{
    /// <summary>Name of the connection string (<c>ConnectionStrings:cadence</c>).</summary>
    public const string ConnectionStringName = "cadence";

    private ITenantContext? _tenant;

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    IQueryable<UserSummary> ICadenceDbContext.Users => Set<UserSummary>().AsNoTracking();

    /// <summary>Evaluated by the tenant query filters on every query.</summary>
    private Guid TenantId => _tenant is { IsResolved: true } tenant ? tenant.OrganizationId : Guid.Empty;

    /// <summary>
    /// Binds this (pooled) context instance to the request's tenant. The tenant is read lazily on each
    /// query, so it may be resolved after the context was created.
    /// </summary>
    internal void UseTenant(ITenantContext? tenant) => _tenant = tenant;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantBoundary();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantBoundary();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CadenceDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes().Where(type => typeof(ITenantScoped).IsAssignableFrom(type.ClrType)))
        {
            typeof(CadenceDbContext)
                .GetMethod(nameof(AddTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder]);
        }
    }

    private void AddTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantScoped =>
        builder.Entity<TEntity>().HasQueryFilter(QueryFilters.Tenant, entity => entity.OrganizationId == TenantId);

    /// <summary>Defense in depth: a bug in a handler must not be able to write into another organization.</summary>
    private void EnforceTenantBoundary()
    {
        if (_tenant is not { IsResolved: true } tenant)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity.OrganizationId != tenant.OrganizationId)
            {
                throw new InvalidOperationException(
                    $"Refusing to save {entry.Metadata.ClrType.Name} for organization {entry.Entity.OrganizationId} " +
                    $"in a request for organization {tenant.OrganizationId}.");
            }
        }
    }
}
