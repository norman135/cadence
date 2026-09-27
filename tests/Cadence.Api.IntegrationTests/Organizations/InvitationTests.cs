using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.Invitations;
using Cadence.Application.Features.Me;
using Cadence.Domain.Organizations;

namespace Cadence.Api.IntegrationTests.Organizations;

[Collection(nameof(ApiCollection))]
public sealed class InvitationTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task An_invitee_can_preview_the_link_then_sign_up_and_join()
    {
        var owner = await factory.SignUpAsync(displayName: "Olivia");
        var organization = await _client.CreateOrganizationAsync(owner, "Invite Co");
        var email = TestAuth.NewEmail();

        Assert.Equal(HttpStatusCode.Created, (await _client.InviteAsync(owner, organization.Id, email, OrganizationRole.Admin)).StatusCode);
        var token = await factory.InvitationTokenAsync(email);

        var preview = await (await _client.GetAsync(new Uri($"/api/v1/invitations/{token}", UriKind.Relative), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<InvitationPreviewResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(("Invite Co", "Olivia", OrganizationRole.Admin, InvitationStatus.Pending), (preview!.OrganizationName, preview.InvitedByName, preview.Role, preview.Status));

        var invitee = await factory.SignUpAsync(email);
        var accepted = await _client.SendAsync(invitee.Authorized(HttpMethod.Post, $"/api/v1/invitations/{token}/accept"), TestContext.Current.CancellationToken);
        var joined = await accepted.Content.ReadJsonAsync<AcceptInvitationResponse>(TestContext.Current.CancellationToken);
        Assert.Equal((organization.Id, OrganizationRole.Admin), (joined!.OrganizationId, joined.Role));

        var me = await (await _client.SendAsync(invitee.Authorized(HttpMethod.Get, "/api/v1/me"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken);
        Assert.Contains(me!.Organizations, org => org.Id == organization.Id && org.Role == OrganizationRole.Admin);

        // Used links cannot be used again.
        await (await _client.SendAsync(invitee.Authorized(HttpMethod.Post, $"/api/v1/invitations/{token}/accept"), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.Conflict, "invitations.not_pending");
    }

    [Fact]
    public async Task Invitations_only_work_for_the_invited_email()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var email = TestAuth.NewEmail();
        await _client.InviteAsync(owner, organization.Id, email, OrganizationRole.Member);
        var token = await factory.InvitationTokenAsync(email);
        var someoneElse = await factory.SignUpAsync();

        var response = await _client.SendAsync(someoneElse.Authorized(HttpMethod.Post, $"/api/v1/invitations/{token}/accept"), TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, "invitations.email_mismatch");
    }

    [Fact]
    public async Task Revoked_and_reissued_invitations_invalidate_older_links()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var email = TestAuth.NewEmail();
        await _client.InviteAsync(owner, organization.Id, email, OrganizationRole.Member);
        var firstToken = await factory.InvitationTokenAsync(email);

        // Inviting the same address again revokes the first link.
        var reissued = await (await _client.InviteAsync(owner, organization.Id, email, OrganizationRole.Guest))
            .Content.ReadJsonAsync<InvitationResponse>(TestContext.Current.CancellationToken);
        var invitee = await factory.SignUpAsync(email);
        await (await _client.SendAsync(invitee.Authorized(HttpMethod.Post, $"/api/v1/invitations/{firstToken}/accept"), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.Conflict, "invitations.not_pending");

        // Revoking the second one removes it from the pending list and kills its link too.
        var revoke = await _client.SendAsync(owner.Authorized(HttpMethod.Delete, $"/api/v1/organizations/{organization.Id}/invitations/{reissued!.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var pending = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"/api/v1/organizations/{organization.Id}/invitations"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<List<InvitationResponse>>(TestContext.Current.CancellationToken);
        Assert.Empty(pending!);
    }

    [Fact]
    public async Task Existing_members_cannot_be_invited_again()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);

        var response = await _client.InviteAsync(owner, organization.Id, owner.Email, OrganizationRole.Member);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "invitations.already_member");
    }

    [Fact]
    public async Task Only_owners_can_invite_owners()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var admin = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Admin);

        var response = await _client.InviteAsync(admin, organization.Id, TestAuth.NewEmail(), OrganizationRole.Owner);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, "members.owner_required");
    }

    [Fact]
    public async Task Unknown_invitation_links_return_404()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/invitations/not-a-real-token", UriKind.Relative), TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "invitations.not_found");
    }
}
