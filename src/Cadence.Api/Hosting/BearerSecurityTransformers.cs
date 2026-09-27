using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cadence.Api.Hosting;

/// <summary>
/// Describes bearer authentication in the OpenAPI document and marks each operation that requires a
/// signed-in user, so API explorers can send the token and clients know which calls need it.
/// </summary>
internal static class BearerSecurityTransformers
{
    private const string SchemeName = "Bearer";

    public static OpenApiOptions AddBearerSecurity(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token from POST /api/v1/auth/login or /api/v1/auth/refresh.",
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var requiresUser = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

            if (requiresUser)
            {
                operation.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
                    },
                ];
            }

            return Task.CompletedTask;
        });

        return options;
    }
}
