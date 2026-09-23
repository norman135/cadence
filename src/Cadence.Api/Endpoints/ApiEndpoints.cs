namespace Cadence.Api.Endpoints;

internal static class ApiEndpoints
{
    /// <summary>Maps every versioned API endpoint under <c>/api/v{version}</c>.</summary>
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.NewVersionedApi("Cadence")
            .MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(1);

        v1.MapSystemEndpoints();

        // Unknown API routes get a JSON 404 instead of falling through to the SPA's index.html.
        app.MapFallback("/api/{**path}", static () => TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: "No API endpoint matches the requested path."));

        return app;
    }
}
