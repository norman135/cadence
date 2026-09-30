using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Common;
using FluentValidation;
using Mediator;

namespace Cadence.Application.Features.Auth;

/// <summary>
/// Emails a password reset link. Always succeeds, whether or not the account exists, so the endpoint
/// cannot be used to discover registered addresses.
/// </summary>
public sealed record ForgotPasswordCommand(string Email) : ICommand<Result>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(command => command.Email).ValidEmail();
}

public sealed class ForgotPasswordCommandHandler(IIdentityService identity, IAccountEmails emails)
    : ICommandHandler<ForgotPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await identity.FindByEmailAsync(command.Email, cancellationToken);
        if (user is not null)
        {
            var token = await identity.CreatePasswordResetTokenAsync(user.Id, cancellationToken);
            await emails.SendPasswordResetAsync(user, token, cancellationToken);
        }

        return Result.Success();
    }
}

/// <summary>Sets a new password with the token from the reset email and signs out every session.</summary>
public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : ICommand<Result>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Token).NotEmpty().MaximumLength(2048);
        RuleFor(command => command.NewPassword).ValidPassword();
    }
}

public sealed class ResetPasswordCommandHandler(IIdentityService identity, ISessionService sessions)
    : ICommandHandler<ResetPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var reset = await identity.ResetPasswordAsync(command.Email, command.Token, command.NewPassword, cancellationToken);
        if (reset.IsFailure)
        {
            return reset.Error;
        }

        // Whoever knew the old password must not stay signed in.
        await sessions.EndAllAsync(reset.Value.Id, cancellationToken);
        return Result.Success();
    }
}
