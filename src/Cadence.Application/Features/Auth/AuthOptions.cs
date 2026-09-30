using System.ComponentModel.DataAnnotations;

namespace Cadence.Application.Features.Auth;

/// <summary>Authentication settings (<c>Cadence:Auth</c>).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Cadence:Auth";

    /// <summary>
    /// Secret used to sign access tokens (HMAC-SHA256). At least 32 characters; generate one with
    /// <c>openssl rand -base64 48</c>. Rotating it signs every user out within one access-token lifetime.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "cadence";

    [Required]
    public string Audience { get; set; } = "cadence";

    /// <summary>Lifetime of access tokens. Short, because they cannot be revoked once issued.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Inactivity lifetime of a session: each refresh extends it by this much.</summary>
    [Range(typeof(TimeSpan), "01:00:00", "90.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// How long a just-rotated refresh token is still accepted, so two browser tabs refreshing at the
    /// same moment don't trigger reuse detection and sign the user out.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:01:00")]
    public TimeSpan RefreshReuseGracePeriod { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Whether users must confirm their email address before signing in.</summary>
    public bool RequireConfirmedEmail { get; set; } = true;

    /// <summary>Whether anyone can create an account. Turn off for invitation-only instances.</summary>
    public bool AllowRegistration { get; set; } = true;
}
