using Cadence.Api.Hosting;
using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace Cadence.Api.Endpoints;

/// <summary>A new access token. The refresh token is set as an httpOnly cookie alongside it.</summary>
/// <param name="AccessToken">Bearer token for the Authorization header.</param>
/// <param name="ExpiresAt">When the access token expires; refresh shortly before.</param>
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AddEndpointFilter(static async (context, next) =>
            {
                // Responses carry credentials; no cache may store them.
                context.HttpContext.Response.Headers[HeaderNames.CacheControl] = "no-store";
                return await next(context);
            });

        auth.MapPost("/register", Register)
            .WithName(nameof(Register))
            .WithSummary("Create an account");

        auth.MapPost("/confirm-email", ConfirmEmail)
            .WithName(nameof(ConfirmEmail))
            .WithSummary("Confirm an email address with the emailed token");

        auth.MapPost("/resend-confirmation", ResendConfirmation)
            .WithName(nameof(ResendConfirmation))
            .WithSummary("Send the confirmation email again");

        auth.MapPost("/login", Login)
            .WithName(nameof(Login))
            .WithSummary("Sign in with email and password");

        auth.MapPost("/refresh", Refresh)
            .WithName(nameof(Refresh))
            .WithSummary("Exchange the session cookie for a new access token")
            .RequireRateLimiting(RateLimitPolicies.Refresh);

        auth.MapPost("/logout", Logout)
            .WithName(nameof(Logout))
            .WithSummary("Sign out and end the session")
            .RequireRateLimiting(RateLimitPolicies.Refresh);

        auth.MapPost("/forgot-password", ForgotPassword)
            .WithName(nameof(ForgotPassword))
            .WithSummary("Email a password reset link");

        auth.MapPost("/reset-password", ResetPassword)
            .WithName(nameof(ResetPassword))
            .WithSummary("Set a new password with the emailed token");

        return api;
    }

    private static async Task<Results<Accepted<RegisterResponse>, ProblemHttpResult>> Register(
        RegisterCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Accepted((string?)null, result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmEmail(
        ConfirmEmailCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    private static async Task<Accepted> ResendConfirmation(
        ResendConfirmationCommand command, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Ok<AccessTokenResponse>, ProblemHttpResult>> Login(
        LoginCommand command, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? StartSession(context, result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<AccessTokenResponse>, ProblemHttpResult>> Refresh(
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RefreshSessionCommand(RefreshTokenCookie.Read(context)), cancellationToken);
        if (result.IsFailure)
        {
            RefreshTokenCookie.Clear(context);
            return result.Error.ToProblem();
        }

        return StartSession(context, result.Value);
    }

    private static async Task<NoContent> Logout(ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        await sender.Send(new LogoutCommand(RefreshTokenCookie.Read(context)), cancellationToken);
        RefreshTokenCookie.Clear(context);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted> ForgotPassword(
        ForgotPasswordCommand command, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetPassword(
        ResetPasswordCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    /// <summary>Puts the refresh token in its cookie and returns only the access token.</summary>
    internal static Ok<AccessTokenResponse> StartSession(HttpContext context, SessionTokens tokens)
    {
        RefreshTokenCookie.Write(context, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        return TypedResults.Ok(new AccessTokenResponse(tokens.AccessToken.Token, tokens.AccessToken.ExpiresAt));
    }
}
