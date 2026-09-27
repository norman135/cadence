using Cadence.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work for Cadence, including the ASP.NET Core Identity user store.
/// Entity mappings live in <c>IEntityTypeConfiguration</c> classes in this assembly and are applied automatically.
/// </summary>
public sealed class CadenceDbContext(DbContextOptions<CadenceDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    /// <summary>Name of the connection string (<c>ConnectionStrings:cadence</c>).</summary>
    public const string ConnectionStringName = "cadence";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CadenceDbContext).Assembly);
    }
}
