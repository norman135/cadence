using Cadence.Application.Features.System;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cadence.Api.Endpoints;

internal static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/system").WithTags("System");

        group.MapGet("/info", GetSystemInfo)
            .WithName(nameof(GetSystemInfo))
            .WithSummary("Get information about the running application")
            .WithDescription("Returns the product name, version, hosting environment and current server time.");

        return api;
    }

    private static async Task<Ok<SystemInfoResponse>> GetSystemInfo(ISender sender, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new GetSystemInfoQuery(), cancellationToken));
}
