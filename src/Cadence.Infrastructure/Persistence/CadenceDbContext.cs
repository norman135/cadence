using Microsoft.EntityFrameworkCore;

namespace Cadence.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work for Cadence. Entity mappings live in <c>IEntityTypeConfiguration</c>
/// classes in this assembly and are applied automatically.
/// </summary>
public sealed class CadenceDbContext(DbContextOptions<CadenceDbContext> options) : DbContext(options)
{
    /// <summary>Name of the connection string (<c>ConnectionStrings:cadence</c>).</summary>
    public const string ConnectionStringName = "cadence";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CadenceDbContext).Assembly);
    }
}
