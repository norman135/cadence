using FluentValidation;

namespace Cadence.Application.Features.Auth;

/// <summary>Validation rules shared by the account commands.</summary>
public static class AuthValidationRules
{
    public const int MinPasswordLength = 10;
    public const int MaxPasswordLength = 128;
    public const int MaxEmailLength = 256;
    public const int MaxDisplayNameLength = 100;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(MaxEmailLength).EmailAddress();

    /// <summary>Length is the only requirement, following NIST SP 800-63B: no composition rules.</summary>
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(MinPasswordLength)
            .MaximumLength(MaxPasswordLength);

    public static IRuleBuilderOptions<T, string> ValidDisplayName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Name is required.")
            .MaximumLength(MaxDisplayNameLength);
}
