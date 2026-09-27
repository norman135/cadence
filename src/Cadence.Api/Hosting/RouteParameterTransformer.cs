using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cadence.Api.Hosting;

/// <summary>
/// Documents route parameters that no handler binds. <c>{organizationId}</c> is consumed by tenant
/// resolution middleware rather than by handlers, so the generator would otherwise omit it and
/// produce an invalid document.
/// </summary>
internal static partial class RouteParameterTransformer
{
    public static OpenApiOptions AddUnboundRouteParameters(this OpenApiOptions options) =>
        options.AddOperationTransformer((operation, context, _) =>
        {
            var path = context.Description.RelativePath ?? string.Empty;
            operation.Parameters ??= [];

            foreach (Match match in RouteParameter().Matches(path))
            {
                var name = match.Groups["name"].Value;
                if (operation.Parameters.Any(parameter => parameter.Name == name && parameter.In == ParameterLocation.Path))
                {
                    continue;
                }

                operation.Parameters.Insert(0, new OpenApiParameter
                {
                    Name = name,
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                });
            }

            return Task.CompletedTask;
        });

    [GeneratedRegex(@"\{(?<name>[A-Za-z]+)(:[^}]*)?\}")]
    private static partial Regex RouteParameter();
}
