using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Common;
using FluentValidation;
using Mediator;

namespace Cadence.Application.Features.Auth;

/// <summary>Confirms an email address with the token from the confirmation email.</summary>
public sealed record ConfirmEmailCommand(Guid UserId, string Token) : ICommand<Result>;

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Token).NotEmpty().MaximumLength(2048);
    }
}

public sealed class ConfirmEmailCommandHandler(IIdentityService identity)
    : ICommandHandler<ConfirmEmailCommand, Result>
{
    public async ValueTask<Result> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken) =>
        await identity.ConfirmEmailAsync(command.UserId, command.Token, cancellationToken);
}

/// <summary>
/// Sends a new confirmation email. Always succeeds, whether or not the account exists, so the endpoint
/// cannot be used to discover registered addresses.
/// </summary>
public sealed record ResendConfirmationCommand(string Email) : ICommand<Result>;

public sealed class ResendConfirmationCommandValidator : AbstractValidator<ResendConfirmationCommand>
{
    public ResendConfirmationCommandValidator() => RuleFor(command => command.Email).ValidEmail();
}

public sealed class ResendConfirmationCommandHandler(IIdentityService identity, IAccountEmails emails)
    : ICommandHandler<ResendConfirmationCommand, Result>
{
    public async ValueTask<Result> Handle(ResendConfirmationCommand command, CancellationToken cancellationToken)
    {
        var user = await identity.FindByEmailAsync(command.Email, cancellationToken);
        if (user is { EmailConfirmed: false })
        {
            var token = await identity.CreateEmailConfirmationTokenAsync(user.Id, cancellationToken);
            await emails.SendEmailConfirmationAsync(user, token, cancellationToken);
        }

        return Result.Success();
    }
}
