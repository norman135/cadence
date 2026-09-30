using Cadence.Api.Hosting;
using Cadence.Application.Features.Me;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cadence.Api.Endpoints;

internal static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        var me = api.MapGroup("/me").WithTags("Me");

        me.MapGet("/", GetCurrentUser)
            .WithName(nameof(GetCurrentUser))
            .WithSummary("Get the signed-in user");

        me.MapPatch("/", UpdateProfile)
            .WithName(nameof(UpdateProfile))
            .WithSummary("Update the signed-in user's profile");

        me.MapPost("/password", ChangePassword)
            .WithName(nameof(ChangePassword))
            .WithSummary("Change the password and sign out other sessions")
            .RequireRateLimiting(RateLimitPolicies.Auth);

        return api;
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> GetCurrentUser(
        ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCurrentUserQuery(), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UpdateProfile(
        UpdateProfileCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<AccessTokenResponse>, ProblemHttpResult>> ChangePassword(
        ChangePasswordCommand command, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? AuthEndpoints.StartSession(context, result.Value) : result.Error.ToProblem();
    }
}
