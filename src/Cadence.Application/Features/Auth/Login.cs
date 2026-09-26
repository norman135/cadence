using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Common;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Options;

namespace Cadence.Application.Features.Auth;

/// <summary>Signs in with email and password and starts a new session.</summary>
public sealed record LoginCommand(string Email, string Password) : ICommand<Result<SessionTokens>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(AuthValidationRules.MaxEmailLength);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(AuthValidationRules.MaxPasswordLength);
    }
}

public sealed class LoginCommandHandler(
    IIdentityService identity,
    ISessionService sessions,
    IOptions<AuthOptions> options)
    : ICommandHandler<LoginCommand, Result<SessionTokens>>
{
    public async ValueTask<Result<SessionTokens>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var verified = await identity.CheckPasswordAsync(
            command.Email.Trim(),
            command.Password,
            options.Value.RequireConfirmedEmail,
            cancellationToken);

        if (verified.IsFailure)
        {
            return verified.Error;
        }

        return await sessions.StartAsync(verified.Value, cancellationToken);
    }
}
