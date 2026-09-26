using System.Net;
using System.Net.Http.Json;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.Auth;

namespace Cadence.Api.IntegrationTests.Auth;

[Collection(nameof(ApiCollection))]
public sealed class RegistrationTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Registering_sends_a_confirmation_email_and_sign_in_waits_for_confirmation()
    {
        var email = TestAuth.NewEmail();

        var registered = await _client.RegisterAsync(email);
        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);
        var body = await registered.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        Assert.True(body!.EmailConfirmationRequired);

        await (await _client.LoginAsync(email)).AssertProblemAsync(HttpStatusCode.Forbidden, "auth.email_not_confirmed");

        await _client.ConfirmEmailAsync(factory, email);
        var session = await _client.SignInAsync(email);

        Assert.False(string.IsNullOrEmpty(session.AccessToken));
        Assert.False(string.IsNullOrEmpty(session.RefreshToken));
    }

    [Fact]
    public async Task An_email_can_only_be_registered_once()
    {
        var email = TestAuth.NewEmail();
        await _client.RegisterAsync(email);

        var second = await _client.RegisterAsync(email.ToUpperInvariant());

        await second.AssertProblemAsync(HttpStatusCode.Conflict, "auth.email_taken");
    }

    [Fact]
    public async Task Invalid_registrations_return_field_errors()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "not-an-email", password = "short", displayName = " " },
            TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(TestContext.Current.CancellationToken);
        Assert.Equal(["displayName", "email", "password"], problem!.Errors.Keys.Order());
    }

    [Fact]
    public async Task A_confirmation_token_cannot_be_forged()
    {
        var email = TestAuth.NewEmail();
        await _client.RegisterAsync(email);
        var message = await factory.Emails.WaitForEmailAsync(email, "Confirm your email");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email",
            new { userId = FakeEmailTransport.LinkParameter(message, "userId"), token = "Zm9yZ2Vk" },
            TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "auth.invalid_token");
    }

    [Fact]
    public async Task Registration_can_be_disabled_for_invitation_only_instances()
    {
        using var closed = factory.WithSettings(new Dictionary<string, string?> { ["Cadence:Auth:AllowRegistration"] = "false" });

        var response = await closed.CreateApiClient().RegisterAsync(TestAuth.NewEmail());

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, "auth.registration_disabled");
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
