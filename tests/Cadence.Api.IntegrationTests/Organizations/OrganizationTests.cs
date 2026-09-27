using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Me;
using Cadence.Application.Features.Members;
using Cadence.Domain.Organizations;

namespace Cadence.Api.IntegrationTests.Organizations;

[Collection(nameof(ApiCollection))]
public sealed class OrganizationTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Creating_an_organization_makes_the_creator_its_owner()
    {
        var owner = await factory.SignUpAsync();

        var organization = await _client.CreateOrganizationAsync(owner, "Acme Corp!");

        Assert.Equal(OrganizationRole.Owner, organization.Role);
        Assert.StartsWith("acme-corp", organization.Slug, StringComparison.Ordinal);

        var me = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, "/api/v1/me"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken);
        Assert.Equal([organization.Id], me!.Organizations.Select(org => org.Id));
    }

    [Fact]
    public async Task Slugs_stay_unique_when_names_collide()
    {
        var owner = await factory.SignUpAsync();

        var first = await _client.CreateOrganizationAsync(owner, $"Same {Guid.NewGuid():N}");
        var second = await _client.CreateOrganizationAsync(owner, first.Name);

        Assert.NotEqual(first.Slug, second.Slug);
        Assert.StartsWith(first.Slug, second.Slug, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Permission_checks_are_served_from_cache()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var route = $"/api/v1/organizations/{organization.Id}";
        await _client.SendAsync(owner.Authorized(HttpMethod.Get, route), TestContext.Current.CancellationToken);

        // Warm cache: only the organization query itself runs; the membership lookup costs nothing.
        var response = await factory.Queries.AssertAtMostAsync(1, () => _client.SendAsync(owner.Authorized(HttpMethod.Get, route)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationRole.Guest, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(OrganizationRole.Member, HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(OrganizationRole.Admin, HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.Created)]
    public async Task Each_role_gets_exactly_its_permissions(
        OrganizationRole role, HttpStatusCode listMembers, HttpStatusCode rename, HttpStatusCode invite)
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var member = await factory.AddMemberAsync(owner, organization.Id, role);
        var route = $"/api/v1/organizations/{organization.Id}";

        Assert.Equal(listMembers, (await _client.SendAsync(member.Authorized(HttpMethod.Get, $"{route}/members"), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(rename, (await _client.SendAsync(member.Authorized(HttpMethod.Patch, route, new { name = "Renamed" }), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(invite, (await _client.InviteAsync(member, organization.Id, TestAuth.NewEmail(), OrganizationRole.Member)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(member.Authorized(HttpMethod.Delete, route), TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Owners_can_delete_the_organization_and_members_lose_access()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var member = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Member);
        var route = $"/api/v1/organizations/{organization.Id}";

        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(owner.Authorized(HttpMethod.Delete, route), TestContext.Current.CancellationToken)).StatusCode);

        await (await _client.SendAsync(member.Authorized(HttpMethod.Get, route), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Members_are_listed_in_join_order_one_page_at_a_time()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var first = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Member);
        var second = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Guest);
        var route = $"/api/v1/organizations/{organization.Id}/members";

        var page1 = await factory.Queries.AssertAtMostAsync(1, async () =>
            (await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"{route}?limit=2"))).Content.ReadJsonAsync<KeysetPage<MemberResponse>>())!);
        var page2 = (await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"{route}?limit=2&after={page1.NextCursor}"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<KeysetPage<MemberResponse>>(TestContext.Current.CancellationToken))!;

        Assert.Equal([owner.Email, first.Email], page1.Items.Select(member => member.Email));
        Assert.Equal([second.Email], page2.Items.Select(member => member.Email));
        Assert.Null(page2.NextCursor);
    }

    [Fact]
    public async Task Only_owners_manage_ownership_and_the_last_owner_cannot_leave()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var admin = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Admin);
        var members = $"/api/v1/organizations/{organization.Id}/members";
        var ownerId = await UserIdAsync(owner);
        var adminId = await UserIdAsync(admin);

        // Admins cannot create owners or touch an owner.
        await (await _client.SendAsync(admin.Authorized(HttpMethod.Patch, $"{members}/{adminId}", new { role = "Owner" }), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "members.owner_required");
        await (await _client.SendAsync(admin.Authorized(HttpMethod.Delete, $"{members}/{ownerId}"), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "members.owner_required");

        // The only owner can neither step down nor leave.
        await (await _client.SendAsync(owner.Authorized(HttpMethod.Patch, $"{members}/{ownerId}", new { role = "Admin" }), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.Conflict, "members.last_owner");
        await (await _client.SendAsync(owner.Authorized(HttpMethod.Delete, $"{members}/{ownerId}"), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.Conflict, "members.last_owner");

        // Transfer ownership, then the original owner may leave.
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(owner.Authorized(HttpMethod.Patch, $"{members}/{adminId}", new { role = "Owner" }), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(owner.Authorized(HttpMethod.Delete, $"{members}/{ownerId}"), TestContext.Current.CancellationToken)).StatusCode);
        await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"/api/v1/organizations/{organization.Id}"), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_removed_member_loses_access_immediately()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var member = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Member);
        var route = $"/api/v1/organizations/{organization.Id}";
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(member.Authorized(HttpMethod.Get, route), TestContext.Current.CancellationToken)).StatusCode);

        var removed = await _client.SendAsync(owner.Authorized(HttpMethod.Delete, $"{route}/members/{await UserIdAsync(member)}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await (await _client.SendAsync(member.Authorized(HttpMethod.Get, route), TestContext.Current.CancellationToken))
            .AssertProblemAsync(HttpStatusCode.NotFound);
    }

    private async Task<Guid> UserIdAsync(TestSession session) =>
        (await (await _client.SendAsync(session.Authorized(HttpMethod.Get, "/api/v1/me")))
            .Content.ReadJsonAsync<CurrentUserResponse>())!.Id;
}
