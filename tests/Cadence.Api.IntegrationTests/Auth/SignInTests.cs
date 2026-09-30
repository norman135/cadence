using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace Cadence.Api.IntegrationTests.Auth;

[Collection(nameof(ApiCollection))]
public sealed class SignInTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task The_session_cookie_is_httponly_strict_and_scoped_to_the_auth_endpoints()
    {
        var email = TestAuth.NewEmail();
        await factory.SignUpAsync(email);

        var response = await _client.LoginAsync(email);

        var cookie = TestAuth.RefreshCookie(response);
        Assert.NotNull(cookie);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/api/v1/auth", cookie.Path.ToString());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_passwords_and_unknown_emails_get_the_same_answer()
    {
        var email = TestAuth.NewEmail();
        await factory.SignUpAsync(email);

        await (await _client.LoginAsync(email, "wrong password!!")).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await (await _client.LoginAsync(TestAuth.NewEmail())).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account_even_for_the_right_password()
    {
        var email = TestAuth.NewEmail();
        await factory.SignUpAsync(email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await _client.LoginAsync(email, "wrong password!!");
        }

        await (await _client.LoginAsync(email)).AssertProblemAsync(HttpStatusCode.Forbidden, "auth.locked_out");
    }

    [Fact]
    public async Task Api_endpoints_require_a_valid_access_token()
    {
        var session = await factory.SignUpAsync();

        var anonymous = await _client.GetAsync(new Uri("/api/v1/me", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var tampered = session with { AccessToken = session.AccessToken[..^4] + "AAAA" };
        var rejected = await _client.SendAsync(tampered.Authorized(HttpMethod.Get, "/api/v1/me"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        var authorized = await _client.SendAsync(session.Authorized(HttpMethod.Get, "/api/v1/me"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
    }

    [Fact]
    public async Task Sign_in_is_rate_limited_per_client()
    {
        using var limited = factory.WithSettings(new Dictionary<string, string?> { ["Cadence:RateLimiting:AuthPermitsPerMinute"] = "2" });
        var client = limited.CreateApiClient();

        await client.LoginAsync(TestAuth.NewEmail());
        await client.LoginAsync(TestAuth.NewEmail());
        var third = await client.LoginAsync(TestAuth.NewEmail());

        await third.AssertProblemAsync(HttpStatusCode.TooManyRequests);
        Assert.True(third.Headers.Contains(HeaderNames.RetryAfter));
    }
}
