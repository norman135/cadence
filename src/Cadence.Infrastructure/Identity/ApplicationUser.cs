using Microsoft.AspNetCore.Identity;

namespace Cadence.Infrastructure.Identity;

/// <summary>
/// A user account in the ASP.NET Core Identity store. The user name is always the email address.
/// Ids are UUIDv7 (ADR-0011), assigned when the account is created.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
