using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Cadence.Domain.Common;
using FluentValidation;
using Mediator;

namespace Cadence.Application.Features.Me;

/// <summary>Updates the signed-in user's display name.</summary>
public sealed record UpdateProfileCommand(string DisplayName) : ICommand<Result>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator() => RuleFor(command => command.DisplayName).ValidDisplayName();
}

public sealed class UpdateProfileCommandHandler(IIdentityService identity, ICurrentUser currentUser)
    : ICommandHandler<UpdateProfileCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var updated = await identity.UpdateDisplayNameAsync(
            currentUser.RequiredUserId,
            command.DisplayName.Trim(),
            cancellationToken);

        return updated.IsSuccess ? Result.Success() : updated.Error;
    }
}

/// <summary>
/// Changes the signed-in user's password. Every existing session is ended and a fresh one is started
/// for the caller, so other devices are signed out but the current one is not.
/// </summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<Result<SessionTokens>>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(AuthValidationRules.MaxPasswordLength);
        RuleFor(command => command.NewPassword).ValidPassword()
            .NotEqual(command => command.CurrentPassword).WithMessage("The new password must be different.");
    }
}

public sealed class ChangePasswordCommandHandler(
    IIdentityService identity,
    ISessionService sessions,
    ICurrentUser currentUser)
    : ICommandHandler<ChangePasswordCommand, Result<SessionTokens>>
{
    public async ValueTask<Result<SessionTokens>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequiredUserId;

        var changed = await identity.ChangePasswordAsync(userId, command.CurrentPassword, command.NewPassword, cancellationToken);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        var user = await identity.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return AuthErrors.UserNotFound;
        }

        await sessions.EndAllAsync(userId, cancellationToken);
        return await sessions.StartAsync(user, cancellationToken);
    }
}
