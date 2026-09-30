using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Cadence.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Infrastructure.Identity;

/// <summary><see cref="IIdentityService"/> on top of ASP.NET Core Identity's <see cref="UserManager{TUser}"/>.</summary>
internal sealed class IdentityService(
    UserManager<ApplicationUser> users,
    IPasswordHasher<ApplicationUser> passwordHasher,
    TimeProvider timeProvider) : IIdentityService
{
    // Hash verified when the email is unknown, so failed sign-ins take the same time either way.
    private static readonly ApplicationUser s_timingUser = new();
    private static readonly Lazy<string> s_timingHash =
        new(() => new PasswordHasher<ApplicationUser>().HashPassword(s_timingUser, Guid.NewGuid().ToString()));

    public async Task<Result<AuthUser>> CreateUserAsync(
        string email,
        string password,
        string displayName,
        bool emailConfirmed,
        CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = emailConfirmed,
            DisplayName = displayName,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        var result = await users.CreateAsync(user, password);
        if (result.Succeeded)
        {
            return ToAuthUser(user);
        }

        return result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName")
            ? AuthErrors.EmailTaken
            : ToValidationError(result);
    }

    public Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        users.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AuthUser(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeEmail(email.Trim());
        return users.Users.AsNoTracking()
            .Where(user => user.NormalizedEmail == normalized)
            .Select(user => new AuthUser(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<AuthUser>> CheckPasswordAsync(
        string email,
        string password,
        bool requireConfirmedEmail,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(s_timingUser, s_timingHash.Value, password);
            return AuthErrors.InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user))
        {
            return AuthErrors.LockedOut;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return await users.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidCredentials;
        }

        // Checked only after the password, so unconfirmed accounts can't be discovered by guessing emails.
        if (requireConfirmedEmail && !user.EmailConfirmed)
        {
            return AuthErrors.EmailNotConfirmed;
        }

        if (user.AccessFailedCount > 0)
        {
            await users.ResetAccessFailedCountAsync(user);
        }

        return ToAuthUser(user);
    }

    public async Task<string> CreateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId);
        return TokenEncoding.Encode(await users.GenerateEmailConfirmationTokenAsync(user));
    }

    public async Task<Result> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !TokenEncoding.TryDecode(token, out var decoded))
        {
            return AuthErrors.InvalidToken;
        }

        var result = await users.ConfirmEmailAsync(user, decoded);
        return result.Succeeded ? Result.Success() : AuthErrors.InvalidToken;
    }

    public async Task<string> CreatePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId);
        return TokenEncoding.Encode(await users.GeneratePasswordResetTokenAsync(user));
    }

    public async Task<Result<AuthUser>> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || !TokenEncoding.TryDecode(token, out var decoded))
        {
            return AuthErrors.InvalidToken;
        }

        var result = await users.ResetPasswordAsync(user, decoded, newPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(error => error.Code == "InvalidToken")
                ? AuthErrors.InvalidToken
                : ToValidationError(result);
        }

        // Following the emailed link proves ownership of the address; clear any lockout too.
        user.EmailConfirmed = true;
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);

        return ToAuthUser(user);
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthErrors.UserNotFound;
        }

        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            return Result.Success();
        }

        return result.Errors.Any(error => error.Code == "PasswordMismatch")
            ? AuthErrors.IncorrectPassword
            : ToValidationError(result);
    }

    public async Task<Result<AuthUser>> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthErrors.UserNotFound;
        }

        user.DisplayName = displayName;
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? ToAuthUser(user) : ToValidationError(result);
    }

    private async Task<ApplicationUser> RequireUserAsync(Guid userId) =>
        await users.FindByIdAsync(userId.ToString())
        ?? throw new InvalidOperationException($"User {userId} does not exist.");

    private static AuthUser ToAuthUser(ApplicationUser user) =>
        new(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed);

    private static Error ToValidationError(IdentityResult result) =>
        Error.Validation("auth.invalid_password", string.Join(" ", result.Errors.Select(error => error.Description)));
}
