using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Cadence.Infrastructure.Caching;
using Cadence.Infrastructure.Email;
using Cadence.Infrastructure.Identity;
using Cadence.Infrastructure.Persistence;
using Cadence.Infrastructure.Persistence.Interceptors;
using MailKit.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Cadence.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Size of the DbContext instance pool. Pooled contexts are reused across requests, avoiding
    /// per-request setup cost; 32 comfortably covers the target load while bounding memory.
    /// </summary>
    private const int DbContextPoolSize = 32;

    /// <summary>Registers persistence, identity, sessions, email and database telemetry for the API.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddCadenceDbContext();

        services.AddHealthChecks()
            .AddDbContextCheck<CadenceDbContext>("database", tags: ["ready"]);

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddNpgsql())
            .WithMetrics(metrics => metrics.AddMeter("Npgsql"));

        services.AddCadenceIdentity();
        services.AddEmail();

        // In-process only (ADR-0008): L1 memory cache with stampede protection and tag invalidation.
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 1024 * 1024;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10),
            };
        });
        services.AddSingleton<IMembershipCache, MembershipCache>();

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

        services.AddPooledDbContextFactory<CadenceDbContext>(
            static (serviceProvider, options) => DatabaseConfiguration.Configure(
                options,
                serviceProvider.GetRequiredService<NpgsqlDataSource>(),
                serviceProvider.GetServices<IInterceptor>()),
            DbContextPoolSize);

        // The request-scoped context comes from the pool and is bound to the request's tenant
        // (hosts without tenants, such as the migrator, get an unbound context).
        services.AddScoped(static serviceProvider =>
        {
            var db = serviceProvider.GetRequiredService<IDbContextFactory<CadenceDbContext>>().CreateDbContext();
            db.UseTenant(serviceProvider.GetService<ITenantContext>());
            return db;
        });
        services.AddScoped<ICadenceDbContext>(static serviceProvider => serviceProvider.GetRequiredService<CadenceDbContext>());

        return services;
    }

    private static void AddCadenceIdentity(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // NIST SP 800-63B: require length, not character classes.
                options.Password.RequiredLength = AuthValidationRules.MinPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);

                options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
            })
            .AddEntityFrameworkStores<CadenceDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromDays(1));

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddSingleton<AccessTokenIssuer>();

        // Bearer authentication for API calls. Claims keep their JWT names ("sub", "email", "name").
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, auth) =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = auth.Value.Issuer,
                    ValidAudience = auth.Value.Audience,
                    IssuerSigningKey = AccessTokenIssuer.CreateSigningKey(auth.Value.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "name",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
    }

    private static void AddEmail(this IServiceCollection services)
    {
        services.AddOptions<PublicUrlOptions>()
            .BindConfiguration(PublicUrlOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .PostConfigure<IConfiguration>(UseMailpitInDevelopment)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<EmailQueue>();
        services.AddScoped<IAccountEmails, AccountEmails>();
        services.AddScoped<IOrganizationEmails, OrganizationEmails>();
        services.AddHostedService<EmailDispatcher>();

        services.TryAddSingleton<IEmailTransport>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmailOptions>>();
            return string.IsNullOrWhiteSpace(options.Value.Smtp.Host)
                ? ActivatorUtilities.CreateInstance<LogOnlyEmailTransport>(serviceProvider)
                : new SmtpEmailTransport(options);
        });
    }

    /// <summary>Aspire provides Mailpit as <c>ConnectionStrings:mailpit</c> (<c>Endpoint=smtp://host:port</c>).</summary>
    private static void UseMailpitInDevelopment(EmailOptions options, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(options.Smtp.Host)
            || configuration.GetConnectionString("mailpit") is not { } connectionString)
        {
            return;
        }

        var endpoint = connectionString.Split(';')
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(pair => pair.Length == 2 && pair[0].Equals("Endpoint", StringComparison.OrdinalIgnoreCase))?[1];

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            options.Smtp.Host = uri.Host;
            options.Smtp.Port = uri.Port;
            options.Smtp.Security = SecureSocketOptions.None;
        }
    }
}
