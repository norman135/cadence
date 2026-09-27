using Cadence.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Cadence.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a real PostgreSQL 18 container. The schema is created by
/// running the same migrations production uses. One instance is shared by every test class in the
/// <see cref="ApiCollection"/>.
/// </summary>
public sealed class CadenceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    /// <summary>Counts every database command the API executes; see <see cref="QueryCounter"/>.</summary>
    public QueryCounter Queries { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync(TestContext.Current.CancellationToken);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{CadenceDbContext.ConnectionStringName}", _database.GetConnectionString());
        builder.ConfigureTestServices(services => services.AddSingleton<IInterceptor>(Queries));
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<CadenceApiFactory>;
