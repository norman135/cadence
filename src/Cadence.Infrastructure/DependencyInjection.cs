using Cadence.Infrastructure.Persistence;
using Cadence.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Cadence.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Size of the DbContext instance pool. Pooled contexts are reused across requests, avoiding
    /// per-request setup cost; 32 comfortably covers the target load while bounding memory.
    /// </summary>
    private const int DbContextPoolSize = 32;

    /// <summary>Registers persistence, database health checks and database telemetry for the API.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddCadenceDbContext();

        services.AddHealthChecks()
            .AddDbContextCheck<CadenceDbContext>("database", tags: ["ready"]);

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddNpgsql())
            .WithMetrics(metrics => metrics.AddMeter("Npgsql"));

        return services;
    }

    /// <summary>
    /// Registers the PostgreSQL data source and a pooled <see cref="CadenceDbContext"/>.
    /// The connection string is read lazily, so hosts that never touch the database
    /// (such as build-time OpenAPI generation) start without one.
    /// </summary>
    public static IServiceCollection AddCadenceDbContext(this IServiceCollection services)
    {
        services.AddOptions<DatabaseDiagnosticsOptions>()
            .BindConfiguration(DatabaseDiagnosticsOptions.SectionName);

        services.TryAddSingleton(static serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(CadenceDbContext.ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{CadenceDbContext.ConnectionStringName}' is not configured. " +
                    $"Set the ConnectionStrings__{CadenceDbContext.ConnectionStringName} environment variable.");

            return new NpgsqlDataSourceBuilder(DatabaseConfiguration.ApplyDefaults(connectionString))
                .UseLoggerFactory(serviceProvider.GetService<ILoggerFactory>())
                .Build();
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IInterceptor, SlowQueryInterceptor>());

        services.AddDbContextPool<CadenceDbContext>(
            static (serviceProvider, options) => DatabaseConfiguration.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>(),
                serviceProvider.GetServices<IInterceptor>()),
            DbContextPoolSize);

        return services;
    }
}
