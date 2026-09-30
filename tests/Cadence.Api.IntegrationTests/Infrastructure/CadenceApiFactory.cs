using Cadence.Infrastructure.Email;
using Cadence.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    /// <summary>Every email the API sends.</summary>
    public FakeEmailTransport Emails { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync(TestContext.Current.CancellationToken);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A variant of this API with different settings, sharing the same database.</summary>
    public WebApplicationFactory<Program> WithSettings(IReadOnlyDictionary<string, string?> settings) =>
        WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{CadenceDbContext.ConnectionStringName}", _database.GetConnectionString());
        builder.UseSetting("Cadence:PublicUrl", "http://cadence.test");
        builder.UseSetting("Cadence:Auth:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        // Reuse detection is tested without a grace period; a dedicated test covers the grace period.
        builder.UseSetting("Cadence:Auth:RefreshReuseGracePeriod", "00:00:00");
        // Every test shares one client IP; a dedicated test covers the limits themselves.
        builder.UseSetting("Cadence:RateLimiting:AuthPermitsPerMinute", "100000");
        builder.UseSetting("Cadence:RateLimiting:RefreshPermitsPerMinute", "100000");
        builder.UseSetting("Cadence:RateLimiting:ApiPermitsPerMinute", "100000");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IInterceptor>(Queries);
            services.RemoveAll<IEmailTransport>();
            services.AddSingleton<IEmailTransport>(Emails);
        });
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<CadenceApiFactory>;
