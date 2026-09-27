using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cadence.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace Cadence.Api.IntegrationTests.Infrastructure;

/// <summary>A signed-in test user: the access token and the refresh token from the session cookie.</summary>
public sealed record TestSession(string Email, string Password, string AccessToken, string RefreshToken);

/// <summary>Drives the real auth endpoints to create users and sessions for tests.</summary>
public static class TestAuth
{
    public const string Password = "correct horse battery staple";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    /// <summary>A client that does not store cookies, so tests control exactly which session is sent.</summary>
    public static HttpClient CreateApiClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string displayName = "Test User") =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, displayName });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    /// <summary>Registers, confirms the email through the emailed link, and signs in.</summary>
    public static async Task<TestSession> SignUpAsync(this CadenceApiFactory factory, string? email = null, string displayName = "Test User")
    {
        email ??= NewEmail();
        var client = factory.CreateApiClient();

        (await client.RegisterAsync(email, displayName)).EnsureSuccessStatusCode();
        await client.ConfirmEmailAsync(factory, email);

        return await client.SignInAsync(email);
    }

    public static async Task ConfirmEmailAsync(this HttpClient client, CadenceApiFactory factory, string email)
    {
        var message = await factory.Emails.WaitForEmailAsync(email, "Confirm your email");
        var response = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new
        {
            userId = FakeEmailTransport.LinkParameter(message, "userId"),
            token = FakeEmailTransport.LinkParameter(message, "token"),
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task<TestSession> SignInAsync(this HttpClient client, string email, string password = Password)
    {
        var response = await client.LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ToSessionAsync(response, email, password);
    }

    public static async Task<TestSession> ToSessionAsync(HttpResponseMessage response, string email, string password = Password)
    {
        var body = await response.Content.ReadJsonAsync<AccessTokenResponse>();
        return new TestSession(email, password, body!.AccessToken, RefreshCookie(response)!.Value.ToString());
    }

    /// <summary>The refresh-token cookie set by a response, if any.</summary>
    public static SetCookieHeaderValue? RefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList()).FirstOrDefault(cookie => cookie.Name == "cadence_refresh")
            : null;

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add(HeaderNames.Cookie, $"cadence_refresh={refreshToken}");
        return client.SendAsync(request);
    }

    public static HttpRequestMessage Authorized(this TestSession session, HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
