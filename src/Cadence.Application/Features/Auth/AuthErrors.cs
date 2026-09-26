using Cadence.Domain.Common;

namespace Cadence.Application.Features.Auth;

/// <summary>Errors of the authentication flows. Codes are stable and used by the frontend.</summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "The email or password is incorrect.");

    public static readonly Error EmailNotConfirmed =
        Error.Forbidden("auth.email_not_confirmed", "Confirm your email address before signing in.");

    public static readonly Error LockedOut =
        Error.Forbidden("auth.locked_out", "Too many failed attempts. Try again in a few minutes.");

    public static readonly Error EmailTaken =
        Error.Conflict("auth.email_taken", "An account with this email address already exists.");

    public static readonly Error RegistrationDisabled =
        Error.Forbidden("auth.registration_disabled", "New accounts can only be created by invitation.");

    public static readonly Error InvalidToken =
        Error.Validation("auth.invalid_token", "The link is invalid or has expired.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalid_refresh_token", "The session has expired. Sign in again.");

    public static readonly Error IncorrectPassword =
        Error.Validation("auth.incorrect_password", "The current password is incorrect.");

    public static readonly Error UserNotFound =
        Error.NotFound("auth.user_not_found", "The account no longer exists.");
}
