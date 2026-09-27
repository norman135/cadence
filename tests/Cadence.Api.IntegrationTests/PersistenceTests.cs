using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cadence.Api.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class PersistenceTests(CadenceApiFactory factory)
{
    [Fact]
    public async Task All_migrations_are_applied()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();

        var pending = await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(pending);
    }

    [Fact]
    public async Task The_model_has_no_changes_missing_a_migration()
    {
        // Fails when an entity or mapping changes without `dotnet ef migrations add`.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task The_query_counter_sees_every_database_command()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();

        factory.Queries.Reset();
        await db.Database.ExecuteSqlRawAsync("SELECT 1", TestContext.Current.CancellationToken);
        await db.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, factory.Queries.Count);
    }

    [Fact]
    public void The_data_source_applies_the_small_host_connection_defaults()
    {
        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        var settings = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        Assert.Equal(20, settings.MaxPoolSize);
        Assert.Equal("cadence", settings.ApplicationName);
        Assert.Equal(GssEncryptionMode.Disable, settings.GssEncryptionMode);
    }
}
