namespace Cadence.Api.Hosting;

/// <summary>
/// The refresh token travels only in this cookie, never in response bodies:
/// <list type="bullet">
/// <item><c>HttpOnly</c>: JavaScript cannot read it, so XSS cannot steal the session.</item>
/// <item><c>SameSite=Strict</c>: browsers don't send it on cross-site requests, which prevents CSRF.</item>
/// <item><c>Path=/api/v1/auth</c>: it is sent only to the auth endpoints, not on every API call.</item>
/// <item><c>Secure</c> whenever the request arrived over HTTPS (always in production, behind Caddy).</item>
/// </list>
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "cadence_refresh";
    public const string Path = "/api/v1/auth";

    public static string? Read(HttpContext context) => context.Request.Cookies[Name];

    public static void Write(HttpContext context, string token, DateTimeOffset expiresAt) =>
        context.Response.Cookies.Append(Name, token, CreateOptions(context, expiresAt));

    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(Name, CreateOptions(context, expiresAt: null));

    private static CookieOptions CreateOptions(HttpContext context, DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
