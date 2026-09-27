using Cadence.Domain.Common;

namespace Cadence.Application.Common.Abstractions;

/// <summary>A user account as the application layer sees it; credentials never leave the identity store.</summary>
public sealed record AuthUser(Guid Id, string Email, string DisplayName, bool EmailConfirmed);

/// <summary>
/// User accounts and credentials. Implemented with ASP.NET Core Identity in the infrastructure layer,
/// which provides password hashing, lockout, security stamps and signed, expiring tokens.
/// </summary>
public interface IIdentityService
{
    /// <summary>Creates an account. Fails with <c>auth.email_taken</c> or password validation errors.</summary>
    Task<Result<AuthUser>> CreateUserAsync(
        string email,
        string password,
        string displayName,
        bool emailConfirmed,
        CancellationToken cancellationToken);

    Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies credentials with lockout protection. Unknown emails take the same time as wrong passwords,
    /// so responses don't reveal which accounts exist.
    /// </summary>
    Task<Result<AuthUser>> CheckPasswordAsync(
        string email,
        string password,
        bool requireConfirmedEmail,
        CancellationToken cancellationToken);

    Task<string> CreateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken);

    Task<string> CreatePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<AuthUser>> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken);

    Task<Result> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);

    Task<Result<AuthUser>> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken);
}
