using System.Net;
using System.Net.Http.Json;
using Cadence.Api.IntegrationTests.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace Cadence.Api.IntegrationTests.Auth;

[Collection(nameof(ApiCollection))]
public sealed class SessionTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Refreshing_rotates_the_refresh_token()
    {
        var session = await factory.SignUpAsync();

        var refreshed = await factory.Queries.AssertAtMostAsync(4, () => _client.RefreshAsync(session.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = await TestAuth.ToSessionAsync(refreshed, session.Email);
        Assert.NotEqual(session.RefreshToken, next.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.RefreshAsync(next.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Reusing_an_exchanged_refresh_token_ends_the_whole_session()
    {
        var session = await factory.SignUpAsync();
        var stolen = session.RefreshToken;
        var legitimate = await TestAuth.ToSessionAsync(await _client.RefreshAsync(session.RefreshToken), session.Email);

        // The attacker replays the old token: rejected, and the session is revoked...
        await (await _client.RefreshAsync(stolen)).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");

        // ...so the legitimate client's newer token stops working too.
        await (await _client.RefreshAsync(legitimate.RefreshToken)).AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tabs_refreshing_at_the_same_moment_are_not_mistaken_for_reuse()
    {
        using var withGrace = factory.WithSettings(new Dictionary<string, string?> { ["Cadence:Auth:RefreshReuseGracePeriod"] = "00:00:10" });
        var client = withGrace.CreateApiClient();
        var session = await factory.SignUpAsync();

        var firstTab = await client.RefreshAsync(session.RefreshToken);
        var secondTab = await client.RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, firstTab.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondTab.StatusCode);
        var first = await TestAuth.ToSessionAsync(firstTab, session.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.RefreshAsync(first.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Refreshing_without_a_session_cookie_is_rejected()
    {
        var response = await _client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");
    }

    [Fact]
    public async Task Signing_out_ends_the_session_and_clears_the_cookie()
    {
        var session = await factory.SignUpAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add(HeaderNames.Cookie, $"cadence_refresh={session.RefreshToken}");
        var logout = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.True(TestAuth.RefreshCookie(logout)!.Expires < DateTimeOffset.UtcNow);
        await (await _client.RefreshAsync(session.RefreshToken)).AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resetting_a_forgotten_password_signs_out_every_session()
    {
        var session = await factory.SignUpAsync();

        var requested = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = session.Email }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var message = await factory.Emails.WaitForEmailAsync(session.Email, "Reset your");

        var reset = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = session.Email,
            token = FakeEmailTransport.LinkParameter(message, "token"),
            newPassword = "a brand new passphrase",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        await (await _client.RefreshAsync(session.RefreshToken)).AssertProblemAsync(HttpStatusCode.Unauthorized);
        await (await _client.LoginAsync(session.Email)).AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.OK, (await _client.LoginAsync(session.Email, "a brand new passphrase")).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_whether_an_account_exists()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = TestAuth.NewEmail() }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
