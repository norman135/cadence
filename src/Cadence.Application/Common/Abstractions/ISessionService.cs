using Cadence.Domain.Common;

namespace Cadence.Application.Common.Abstractions;

/// <summary>A short-lived bearer token for API calls.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// The tokens of a signed-in session: an access token for the response body and a refresh token for
/// an httpOnly cookie. The refresh token is opaque and only its hash is stored.
/// </summary>
public sealed record SessionTokens(AccessToken AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Issues and rotates session tokens (ADR-0006).</summary>
public interface ISessionService
{
    /// <summary>Starts a new session (a new refresh-token family) for the user.</summary>
    Task<SessionTokens> StartAsync(AuthUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a refresh token for new tokens and invalidates the old refresh token. Presenting a token
    /// that was already exchanged revokes the whole session, because it means the token was stolen.
    /// </summary>
    Task<Result<SessionTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Ends the session the refresh token belongs to.</summary>
    Task EndAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Ends every session of the user, e.g. after a password change.</summary>
    Task EndAllAsync(Guid userId, CancellationToken cancellationToken);
}
