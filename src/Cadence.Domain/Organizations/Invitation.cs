using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Cadence.Domain.Common;

namespace Cadence.Domain.Organizations;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
    Expired,
}

/// <summary>
/// An invitation for an email address to join an organization with a role. The emailed token is 32
/// random bytes; only its SHA-256 hash is stored, so a database leak does not expose working links.
/// </summary>
public sealed class Invitation : AggregateRoot<Guid>, ITenantScoped
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation(
        Guid id,
        Guid organizationId,
        string email,
        OrganizationRole role,
        byte[] tokenHash,
        Guid invitedByUserId,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + Lifetime;
    }

    /// <summary>Used by EF Core.</summary>
    private Invitation()
    {
        Email = string.Empty;
        TokenHash = [];
    }

    public Guid OrganizationId { get; private init; }

    /// <summary>The invited address, lowercased.</summary>
    public string Email { get; private init; }

    public OrganizationRole Role { get; private init; }

    public byte[] TokenHash { get; private init; }

    public Guid InvitedByUserId { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Creates an invitation and returns it with its plaintext token (for the email link), which is
    /// never stored. Only owners may invite someone as an owner.
    /// </summary>
    public static Result<(Invitation Invitation, string Token)> Create(
        Guid organizationId,
        string email,
        OrganizationRole role,
        OrganizationRole inviterRole,
        Guid invitedByUserId,
        DateTimeOffset now)
    {
        if (role == OrganizationRole.Owner && inviterRole != OrganizationRole.Owner)
        {
            return Result.Failure<(Invitation, string)>(OrganizationErrors.OwnerRequired);
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var invitation = new Invitation(
            Guid.CreateVersion7(),
            organizationId,
            NormalizeEmail(email),
            role,
            HashToken(token),
            invitedByUserId,
            now);

        return (invitation, token);
    }

    public static byte[] HashToken(string token) => SHA256.HashData(Encoding.ASCII.GetBytes(token));

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public InvitationStatus StatusAt(DateTimeOffset now) =>
        AcceptedAt is not null ? InvitationStatus.Accepted
        : RevokedAt is not null ? InvitationStatus.Revoked
        : ExpiresAt <= now ? InvitationStatus.Expired
        : InvitationStatus.Pending;

    /// <summary>Accepts the invitation. It must be pending and addressed to the accepting user's email.</summary>
    public Result Accept(Guid userId, string userEmail, DateTimeOffset now)
    {
        if (StatusAt(now) != InvitationStatus.Pending)
        {
            return OrganizationErrors.InvitationNotPending;
        }

        if (!string.Equals(Email, NormalizeEmail(userEmail), StringComparison.Ordinal))
        {
            return OrganizationErrors.InvitationEmailMismatch;
        }

        AcceptedAt = now;
        AcceptedByUserId = userId;
        return Result.Success();
    }

    public Result Revoke(DateTimeOffset now)
    {
        if (StatusAt(now) != InvitationStatus.Pending)
        {
            return OrganizationErrors.InvitationNotPending;
        }

        RevokedAt = now;
        return Result.Success();
    }
}
