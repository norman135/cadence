using System.Net;
using System.Net.Http.Json;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.Me;

namespace Cadence.Api.IntegrationTests.Auth;

[Collection(nameof(ApiCollection))]
public sealed class ProfileTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task The_signed_in_user_can_read_and_rename_their_profile()
    {
        var session = await factory.SignUpAsync(displayName: "Ada");

        var before = await factory.Queries.AssertAtMostAsync(1, () =>
            _client.SendAsync(session.Authorized(HttpMethod.Get, "/api/v1/me")));
        var profile = await before.Content.ReadFromJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(("Ada", session.Email), (profile!.DisplayName, profile.Email));

        var renamed = await _client.SendAsync(session.Authorized(HttpMethod.Patch, "/api/v1/me", new { displayName = "Ada Lovelace" }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);

        var after = await _client.SendAsync(session.Authorized(HttpMethod.Get, "/api/v1/me"), TestContext.Current.CancellationToken);
        Assert.Equal("Ada Lovelace", (await after.Content.ReadFromJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken))!.DisplayName);
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_ends_the_others()
    {
        var session = await factory.SignUpAsync();
        var otherDevice = await _client.SignInAsync(session.Email);

        var changed = await _client.SendAsync(session.Authorized(
            HttpMethod.Post,
            "/api/v1/me/password",
            new { currentPassword = TestAuth.Password, newPassword = "another long passphrase" }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var current = await TestAuth.ToSessionAsync(changed, session.Email);
        Assert.Equal(HttpStatusCode.OK, (await _client.RefreshAsync(current.RefreshToken)).StatusCode);
        await (await _client.RefreshAsync(otherDevice.RefreshToken)).AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_the_password_requires_the_current_one()
    {
        var session = await factory.SignUpAsync();

        var response = await _client.SendAsync(session.Authorized(
            HttpMethod.Post,
            "/api/v1/me/password",
            new { currentPassword = "not my password", newPassword = "another long passphrase" }), TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "auth.incorrect_password");
    }
}
