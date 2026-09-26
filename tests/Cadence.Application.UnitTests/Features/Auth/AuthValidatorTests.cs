using Cadence.Application.Features.Auth;
using Cadence.Application.Features.Me;

namespace Cadence.Application.UnitTests.Features.Auth;

public sealed class AuthValidatorTests
{
    [Theory]
    [InlineData("ada@example.com", "ten chars!", "Ada", true)]
    [InlineData("ada@example.com", "nine char", "Ada", false)]
    [InlineData("not-an-email", "a long enough password", "Ada", false)]
    [InlineData("ada@example.com", "a long enough password", "   ", false)]
    public void Registration_requires_a_valid_email_a_ten_character_password_and_a_name(
        string email, string password, string displayName, bool valid)
    {
        var result = new RegisterCommandValidator().Validate(new RegisterCommand(email, password, displayName));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Passwords_have_no_composition_rules_only_a_length()
    {
        var lowercaseOnly = new RegisterCommand("ada@example.com", "correct horse battery staple", "Ada");

        Assert.True(new RegisterCommandValidator().Validate(lowercaseOnly).IsValid);
    }

    [Fact]
    public void Display_names_are_limited_to_100_characters()
    {
        var result = new UpdateProfileCommandValidator().Validate(new UpdateProfileCommand(new string('a', 101)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_new_password_must_differ_from_the_current_one()
    {
        var result = new ChangePasswordCommandValidator().Validate(
            new ChangePasswordCommand("same passphrase here", "same passphrase here"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }
}
