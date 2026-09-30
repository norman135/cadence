using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace Cadence.Infrastructure.Email;

/// <summary>Outgoing email settings (<c>Cadence:Email</c>).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Cadence:Email";

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "cadence@localhost";

    [Required]
    public string FromName { get; set; } = "Cadence";

    public SmtpOptions Smtp { get; set; } = new();
}

/// <summary>SMTP server settings. When no host is configured, emails are logged and dropped.</summary>
public sealed class SmtpOptions
{
    public string? Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary><c>Auto</c>, <c>None</c>, <c>StartTls</c> or <c>SslOnConnect</c>.</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;
}

/// <summary>Where users reach Cadence (<c>Cadence:PublicUrl</c>), used for links in emails.</summary>
public sealed class PublicUrlOptions
{
    public const string SectionName = "Cadence";

    [Required]
    [Url]
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>Builds an absolute link to a frontend route with query parameters.</summary>
    public string Link(string path, params (string Name, string Value)[] query)
    {
        var baseUrl = PublicUrl.TrimEnd('/');
        var queryString = string.Join('&', query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Name)}={Uri.EscapeDataString(pair.Value)}"));

        return queryString.Length == 0 ? $"{baseUrl}{path}" : $"{baseUrl}{path}?{queryString}";
    }
}
