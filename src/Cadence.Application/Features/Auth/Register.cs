using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Common;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Options;

namespace Cadence.Application.Features.Auth;

/// <summary>Creates an account and, when confirmation is required, emails a confirmation link.</summary>
public sealed record RegisterCommand(string Email, string Password, string DisplayName)
    : ICommand<Result<RegisterResponse>>;

/// <param name="EmailConfirmationRequired">Whether the user must confirm their email before signing in.</param>
public sealed record RegisterResponse(bool EmailConfirmationRequired);

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).ValidPassword();
        RuleFor(command => command.DisplayName).ValidDisplayName();
    }
}

public sealed class RegisterCommandHandler(
    IIdentityService identity,
    IAccountEmails emails,
    IOptions<AuthOptions> options)
    : ICommandHandler<RegisterCommand, Result<RegisterResponse>>
{
    public async ValueTask<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.AllowRegistration)
        {
            return AuthErrors.RegistrationDisabled;
        }

        var created = await identity.CreateUserAsync(
            command.Email.Trim(),
            command.Password,
            command.DisplayName.Trim(),
            emailConfirmed: !settings.RequireConfirmedEmail,
            cancellationToken);

        if (created.IsFailure)
        {
            return created.Error;
        }

        if (settings.RequireConfirmedEmail)
        {
            var user = created.Value;
            var token = await identity.CreateEmailConfirmationTokenAsync(user.Id, cancellationToken);
            await emails.SendEmailConfirmationAsync(user, token, cancellationToken);
        }

        return new RegisterResponse(settings.RequireConfirmedEmail);
    }
}
