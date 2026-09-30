using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Members;
using Cadence.Domain.Organizations;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cadence.Api.IntegrationTests.Organizations;

/// <summary>Two organizations side by side must never see or touch each other's data (ADR-0007).</summary>
[Collection(nameof(ApiCollection))]
public sealed class TenantIsolationTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Outsiders_get_404_for_every_route_of_another_organization()
    {
        var alice = await factory.SignUpAsync();
        var bob = await factory.SignUpAsync();
        var bobs = await _client.CreateOrganizationAsync(bob, "Bob's Org");

        string[] routes =
        [
            $"/api/v1/organizations/{bobs.Id}",
            $"/api/v1/organizations/{bobs.Id}/members",
            $"/api/v1/organizations/{bobs.Id}/invitations",
        ];

        foreach (var route in routes)
        {
            var response = await _client.SendAsync(alice.Authorized(HttpMethod.Get, route), TestContext.Current.CancellationToken);
            await response.AssertProblemAsync(HttpStatusCode.NotFound, "organizations.not_found");
        }

        var rename = await _client.SendAsync(alice.Authorized(HttpMethod.Patch, $"/api/v1/organizations/{bobs.Id}", new { name = "Hijacked" }), TestContext.Current.CancellationToken);
        await rename.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Member_lists_contain_only_the_requested_organization()
    {
        var alice = await factory.SignUpAsync();
        var bob = await factory.SignUpAsync();
        var alices = await _client.CreateOrganizationAsync(alice, "Alice's Org");
        await _client.CreateOrganizationAsync(bob, "Bob's Org");

        var response = await _client.SendAsync(alice.Authorized(HttpMethod.Get, $"/api/v1/organizations/{alices.Id}/members"), TestContext.Current.CancellationToken);
        var page = await response.Content.ReadJsonAsync<KeysetPage<MemberResponse>>(TestContext.Current.CancellationToken);

        var member = Assert.Single(page!.Items);
        Assert.Equal(alice.Email, member.Email);
    }

    [Fact]
    public async Task Tenant_scoped_queries_without_a_resolved_organization_return_nothing()
    {
        var owner = await factory.SignUpAsync();
        await _client.CreateOrganizationAsync(owner);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();

        Assert.Empty(await db.Memberships.ToListAsync(TestContext.Current.CancellationToken));
        Assert.NotEmpty(await db.Memberships.IgnoreQueryFilters([QueryFilters.Tenant]).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Saving_into_another_organization_is_refused()
    {
        var owner = await factory.SignUpAsync();
        var mine = await _client.CreateOrganizationAsync(owner, "Mine");
        var other = await _client.CreateOrganizationAsync(owner, "Other");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CadenceDbContext>>().CreateDbContext();
        db.UseTenant(new FixedTenant(mine.Id));

        db.Memberships.Add(Membership.Create(other.Id, Guid.CreateVersion7(), OrganizationRole.Member, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FixedTenant(Guid organizationId) : ITenantContext
    {
        public bool IsResolved => true;

        public Guid OrganizationId => organizationId;

        public OrganizationRole Role => OrganizationRole.Owner;
    }
}
