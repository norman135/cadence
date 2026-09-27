using System.Net;
using Cadence.Application.Features.Organizations;
using Cadence.Domain.Organizations;

namespace Cadence.Api.IntegrationTests.Infrastructure;

/// <summary>Drives the real organization and invitation endpoints to set up test scenarios.</summary>
public static class TestOrganizations
{
    public static async Task<OrganizationResponse> CreateOrganizationAsync(this HttpClient client, TestSession owner, string name = "Acme")
    {
        var response = await client.SendAsync(owner.Authorized(HttpMethod.Post, "/api/v1/organizations", new { name }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadJsonAsync<OrganizationResponse>())!;
    }

    public static Task<HttpResponseMessage> InviteAsync(this HttpClient client, TestSession inviter, Guid organizationId, string email, OrganizationRole role) =>
        client.SendAsync(inviter.Authorized(
            HttpMethod.Post,
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email, role = role.ToString() }));

    /// <summary>The token from the latest invitation email to <paramref name="email"/>.</summary>
    public static async Task<string> InvitationTokenAsync(this CadenceApiFactory factory, string email)
    {
        var message = await factory.Emails.WaitForEmailAsync(email, "invited you");
        return FakeEmailTransport.LinkPathSegment(message);
    }

    /// <summary>Invites a new user, signs them up and accepts the invitation: a ready-made member.</summary>
    public static async Task<TestSession> AddMemberAsync(this CadenceApiFactory factory, TestSession inviter, Guid organizationId, OrganizationRole role)
    {
        var client = factory.CreateApiClient();
        var email = TestAuth.NewEmail();

        Assert.Equal(HttpStatusCode.Created, (await client.InviteAsync(inviter, organizationId, email, role)).StatusCode);
        var token = await factory.InvitationTokenAsync(email);
        var member = await factory.SignUpAsync(email, $"{role} User");

        var accepted = await client.SendAsync(member.Authorized(HttpMethod.Post, $"/api/v1/invitations/{token}/accept"));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return member;
    }
}
