using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cadence.Infrastructure.Identity;

/// <summary>
/// Password reset tokens expire after 2 hours, much sooner than email confirmation tokens (1 day),
/// because a leaked reset link grants access to the account.
/// </summary>
internal sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "PasswordReset";
}

internal sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        Name = PasswordResetTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromHours(2);
    }
}
