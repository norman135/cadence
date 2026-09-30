using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Cadence.Infrastructure.Identity;

public enum RefreshTokenRevocationReason
{
    /// <summary>Exchanged for a newer token in the same session.</summary>
    Rotated,

    /// <summary>The user signed out.</summary>
    Logout,

    /// <summary>An already-exchanged token was presented again; the whole session was ended.</summary>
    Reuse,

    /// <summary>All sessions were ended, e.g. after a password change or reset.</summary>
    SessionsEnded,
}

/// <summary>
/// One refresh token in a session. Every refresh replaces the token with a new one in the same
/// <see cref="FamilyId"/> (the session). Only a SHA-256 hash is stored: a database leak does not
/// reveal usable tokens.
/// </summary>
public sealed class RefreshToken
{
    private const int TokenBytes = 32;

    private RefreshToken()
    {
    }

    public Guid Id { get; private init; }

    public Guid UserId { get; private init; }

    /// <summary>Identifies the session; shared by every token rotated from the same sign-in.</summary>
    public Guid FamilyId { get; private init; }

    public byte[] TokenHash { get; private init; } = [];

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RefreshTokenRevocationReason? RevocationReason { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>PostgreSQL <c>xmin</c>, for optimistic concurrency when two refreshes race.</summary>
    public uint Version { get; private set; }

    /// <summary>Creates a token and returns it with its plaintext value, which is never stored.</summary>
    public static (RefreshToken Entity, string Token) Issue(Guid userId, Guid familyId, DateTimeOffset now, TimeSpan lifetime)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

        var entity = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };

        return (entity, token);
    }

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.ASCII.GetBytes(token));

    public void Revoke(DateTimeOffset now, RefreshTokenRevocationReason reason, Guid? replacedBy = null)
    {
        RevokedAt = now;
        RevocationReason = reason;
        ReplacedByTokenId = replacedBy;
    }
}
