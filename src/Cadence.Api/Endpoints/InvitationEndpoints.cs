using Cadence.Api.Hosting;
using Cadence.Application.Features.Invitations;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cadence.Api.Endpoints;

/// <summary>Invitation links, used by the invitee rather than by the organization.</summary>
internal static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationEndpoints(this RouteGroupBuilder api)
    {
        var invitations = api.MapGroup("/invitations/{token}").WithTags("Invitations");

        // The token is the credential; rate-limited like sign-in so tokens can't be guessed at speed.
        invitations.MapGet("/", GetInvitation)
            .WithName(nameof(GetInvitation))
            .WithSummary("Preview an invitation")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        invitations.MapPost("/accept", AcceptInvitation)
            .WithName(nameof(AcceptInvitation))
            .WithSummary("Accept an invitation and join the organization")
            .RequireRateLimiting(RateLimitPolicies.Auth);

        return api;
    }

    private static async Task<Results<Ok<InvitationPreviewResponse>, ProblemHttpResult>> GetInvitation(
        string token, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInvitationQuery(token), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<AcceptInvitationResponse>, ProblemHttpResult>> AcceptInvitation(
        string token, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AcceptInvitationCommand(token), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
