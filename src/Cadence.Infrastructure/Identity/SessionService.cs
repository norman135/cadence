using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Cadence.Domain.Common;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cadence.Infrastructure.Identity;

/// <summary>
/// Sessions as families of rotating refresh tokens (ADR-0006). Each refresh token can be exchanged
/// once. Presenting an exchanged token again means it was copied, so the whole family is revoked:
/// the thief and the victim are both signed out, and the thief's copy becomes useless.
/// </summary>
internal sealed partial class SessionService(
    CadenceDbContext db,
    AccessTokenIssuer accessTokens,
    IIdentityService identity,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider,
    ILogger<SessionService> logger) : ISessionService
{
    private readonly AuthOptions _options = options.Value;

    public async Task<SessionTokens> StartAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var (entity, token) = RefreshToken.Issue(
            user.Id,
            familyId: Guid.CreateVersion7(),
            timeProvider.GetUtcNow(),
            _options.RefreshTokenLifetime);

        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return new SessionTokens(accessTokens.Issue(user), token, entity.ExpiresAt);
    }

    public async Task<Result<SessionTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            return await RefreshOnceAsync(refreshToken, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request rotated the same token a moment ago; re-evaluate against the latest state,
            // where the grace period applies.
            db.ChangeTracker.Clear();
            return await RefreshOnceAsync(refreshToken, cancellationToken);
        }
    }

    public async Task EndAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = RefreshToken.Hash(refreshToken);
        var familyId = await db.RefreshTokens
            .Where(token => token.TokenHash == hash)
            .Select(token => (Guid?)token.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (familyId is { } family)
        {
            await RevokeFamilyAsync(family, RefreshTokenRevocationReason.Logout, cancellationToken);
        }
    }

    public Task EndAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, RefreshTokenRevocationReason.SessionsEnded),
                cancellationToken);
    }

    private async Task<Result<SessionTokens>> RefreshOnceAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hash = RefreshToken.Hash(refreshToken);
        var current = await db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (current is null || current.ExpiresAt <= now)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        if (current.RevokedAt is { } revokedAt)
        {
            var concurrentRefresh = current.RevocationReason == RefreshTokenRevocationReason.Rotated
                && now - revokedAt <= _options.RefreshReuseGracePeriod;

            if (!concurrentRefresh)
            {
                await RevokeFamilyAsync(current.FamilyId, RefreshTokenRevocationReason.Reuse, cancellationToken);
                LogReuseDetected(logger, current.UserId, current.FamilyId);
                return AuthErrors.InvalidRefreshToken;
            }
        }

        var user = await identity.FindByIdAsync(current.UserId, cancellationToken);
        if (user is null)
        {
            await RevokeFamilyAsync(current.FamilyId, RefreshTokenRevocationReason.SessionsEnded, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        var (next, token) = RefreshToken.Issue(user.Id, current.FamilyId, now, _options.RefreshTokenLifetime);
        db.RefreshTokens.Add(next);

        if (current.RevokedAt is null)
        {
            current.Revoke(now, RefreshTokenRevocationReason.Rotated, next.Id);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new SessionTokens(accessTokens.Issue(user), token, next.ExpiresAt);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, RefreshTokenRevocationReason reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, reason),
                cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; session {FamilyId} revoked")]
    private static partial void LogReuseDetected(ILogger logger, Guid userId, Guid familyId);
}
