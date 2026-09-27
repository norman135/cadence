using System.Diagnostics;
using Asp.Versioning;
using Cadence.Api.Serialization;
using Cadence.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Cadence.Api.Hosting;

internal static class ApiServiceCollectionExtensions
{
    /// <summary>Registers HTTP-level services: serialization, errors, versioning, OpenAPI and data protection.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IApplicationInfo, ApplicationInfo>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<HttpTenantContext>();
        services.AddScoped<ITenantContext>(static serviceProvider => serviceProvider.GetRequiredService<HttpTenantContext>());
        services.AddCadenceRateLimiting();

        // Source-generated serialization for API contracts; the reflection resolver remains as a fallback
        // for framework types such as ProblemDetails.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonSerializerContext.Default));

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        });
        services.AddExceptionHandler<ValidationExceptionHandler>();

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi(options => options.Document
                .AddBearerSecurity()
                .AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = "Cadence API";
                    document.Info.Description = "Project and work management for teams.";
                    return Task.CompletedTask;
                }));

        // Keys protect auth cookies and tokens. In containers they must live on a volume,
        // otherwise every restart would sign all users out.
        var dataProtection = services.AddDataProtection().SetApplicationName("Cadence");
        var keysDirectory = configuration["Cadence:DataProtection:KeysDirectory"];
        if (!string.IsNullOrWhiteSpace(keysDirectory))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
        }

        services.Configure<KestrelServerOptions>(options => options.AddServerHeader = false);

        return services;
    }
}
