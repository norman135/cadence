using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Cadence.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> create the DbContext without starting the API host.
/// Authoring migrations does not connect to a database, so a placeholder connection string is enough.
/// </summary>
/// <example><c>dotnet ef migrations add AddIssues --project src/Cadence.Infrastructure</c></example>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CadenceDbContext>
{
    public CadenceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__cadence")
            ?? "Host=localhost;Database=cadence;Username=cadence;Password=design-time-only";

        var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
        var options = DatabaseConfiguration.Configure(new DbContextOptionsBuilder<CadenceDbContext>(), dataSource, []);

        return new CadenceDbContext((DbContextOptions<CadenceDbContext>)options.Options);
    }
}
