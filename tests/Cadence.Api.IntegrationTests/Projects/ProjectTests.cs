using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.Projects;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using static Cadence.Api.IntegrationTests.Infrastructure.TestProjects;

namespace Cadence.Api.IntegrationTests.Projects;

[Collection(nameof(ApiCollection))]
public sealed class ProjectTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task The_creator_becomes_the_admin_and_keys_are_suggested_and_kept_unique()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);

        var first = await _client.CreateProjectAsync(owner, organization.Id, "Atlas Mobile");
        var second = await _client.CreateProjectAsync(owner, organization.Id, "Atlas Mobile");

        Assert.Equal("AM", first.Key);
        Assert.Equal("AM2", second.Key);
        Assert.Equal(ProjectRole.Admin, first.Role);
    }

    [Fact]
    public async Task Requested_keys_must_be_free_and_well_formed()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        await _client.CreateProjectAsync(owner, organization.Id, "Atlas", key: "ATL");

        var taken = await _client.SendAsync(owner.Authorized(HttpMethod.Post, $"{OrganizationUrl(organization.Id)}/projects", new { name = "Other", key = "atl" }), TestContext.Current.CancellationToken);
        await taken.AssertProblemAsync(HttpStatusCode.Conflict, "projects.key_taken");

        var malformed = await _client.SendAsync(owner.Authorized(HttpMethod.Post, $"{OrganizationUrl(organization.Id)}/projects", new { name = "Other", key = "9-X" }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task Members_see_only_their_projects_while_admins_see_all()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var member = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Member);
        var memberId = await _client.UserIdAsync(member);

        var shared = await _client.CreateProjectAsync(owner, organization.Id, "Shared", "SHR");
        var hidden = await _client.CreateProjectAsync(owner, organization.Id, "Hidden", "HID");
        await _client.AddProjectMemberAsync(owner, organization.Id, shared.Id, memberId, ProjectRole.Viewer);

        var memberSees = await ListAsync(member, organization.Id);
        Assert.Equal(["SHR"], memberSees.Select(project => project.Key));
        Assert.Equal(ProjectRole.Viewer, memberSees[0].Role);

        var ownerSees = await ListAsync(owner, organization.Id);
        Assert.Equal(["HID", "SHR"], ownerSees.Select(project => project.Key));

        // A project the member can't see answers 404, the same as one that doesn't exist.
        var get = await _client.SendAsync(member.Authorized(HttpMethod.Get, $"{OrganizationUrl(organization.Id)}/projects/{hidden.Id}"), TestContext.Current.CancellationToken);
        await get.AssertProblemAsync(HttpStatusCode.NotFound, "projects.not_found");
    }

    [Fact]
    public async Task Only_project_admins_change_the_project()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var member = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Member);
        var project = await _client.CreateProjectAsync(owner, organization.Id);
        await _client.AddProjectMemberAsync(owner, organization.Id, project.Id, await _client.UserIdAsync(member), ProjectRole.Member);

        var url = $"{OrganizationUrl(organization.Id)}/projects/{project.Id}";
        var denied = await _client.SendAsync(member.Authorized(HttpMethod.Patch, url, new { name = "Renamed" }), TestContext.Current.CancellationToken);
        await denied.AssertProblemAsync(HttpStatusCode.Forbidden, "projects.forbidden");

        var renamed = await _client.SendAsync(owner.Authorized(HttpMethod.Patch, url, new { name = "Renamed", description = "The app" }), TestContext.Current.CancellationToken);
        var body = await renamed.Content.ReadJsonAsync<ProjectResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(("Renamed", "The app"), (body!.Name, body.Description));
    }

    [Fact]
    public async Task Archived_projects_leave_the_list_and_take_no_new_issues()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var project = await _client.CreateProjectAsync(owner, organization.Id);

        var archived = await _client.SendAsync(owner.Authorized(HttpMethod.Post, $"{OrganizationUrl(organization.Id)}/projects/{project.Id}/archive"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        Assert.Empty(await ListAsync(owner, organization.Id));
        Assert.Single(await ListAsync(owner, organization.Id, includeArchived: true));

        var create = await _client.CreateIssueResponseAsync(owner, organization.Id, new { projectId = project.Id, title = "Too late" });
        await create.AssertProblemAsync(HttpStatusCode.Conflict, "projects.archived");
    }

    [Fact]
    public async Task Project_members_are_organization_members_and_one_admin_always_remains()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var ownerId = await _client.UserIdAsync(owner);
        var project = await _client.CreateProjectAsync(owner, organization.Id);
        var membersUrl = $"{OrganizationUrl(organization.Id)}/projects/{project.Id}/members";

        // Someone from outside the organization can't be added.
        var outsider = await factory.SignUpAsync();
        var addOutsider = await _client.SendAsync(owner.Authorized(HttpMethod.Post, membersUrl, new { userId = await _client.UserIdAsync(outsider), role = "Member" }), TestContext.Current.CancellationToken);
        await addOutsider.AssertProblemAsync(HttpStatusCode.BadRequest, "projects.not_in_organization");

        // The creator is the only explicit admin, so they can't step down.
        var demote = await _client.SendAsync(owner.Authorized(HttpMethod.Patch, $"{membersUrl}/{ownerId}", new { role = "Member" }), TestContext.Current.CancellationToken);
        await demote.AssertProblemAsync(HttpStatusCode.Conflict, "projects.last_admin");

        var people = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, membersUrl), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<List<ProjectMemberResponse>>(TestContext.Current.CancellationToken);
        Assert.Equal([(ownerId, ProjectRole.Admin, false)], people!.Select(person => (person.UserId, person.Role, person.IsImplicit)));
    }

    [Fact]
    public async Task Organization_admins_have_implicit_access_to_every_project()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var admin = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Admin);
        var project = await _client.CreateProjectAsync(owner, organization.Id);

        var seen = await ListAsync(admin, organization.Id);
        Assert.Equal(ProjectRole.Admin, Assert.Single(seen).Role);

        var people = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"{OrganizationUrl(organization.Id)}/projects/{project.Id}/members"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<List<ProjectMemberResponse>>(TestContext.Current.CancellationToken);
        var adminId = await _client.UserIdAsync(admin);
        Assert.Contains(people!, person => person.UserId == adminId && person.IsImplicit && person.Role == ProjectRole.Admin);
    }

    [Fact]
    public async Task Leaving_the_organization_ends_project_access_immediately()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var member = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Member);
        var memberId = await _client.UserIdAsync(member);
        var project = await _client.CreateProjectAsync(owner, organization.Id);
        await _client.AddProjectMemberAsync(owner, organization.Id, project.Id, memberId, ProjectRole.Member);
        Assert.Single(await ListAsync(member, organization.Id));

        var removed = await _client.SendAsync(owner.Authorized(HttpMethod.Delete, $"{OrganizationUrl(organization.Id)}/members/{memberId}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        // Re-invited later, they start without project access.
        var people = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"{OrganizationUrl(organization.Id)}/projects/{project.Id}/members"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<List<ProjectMemberResponse>>(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(people!, person => person.UserId == memberId);
    }

    [Fact]
    public async Task Listing_projects_is_one_query_with_a_warm_cache()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var member = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Member);
        var project = await _client.CreateProjectAsync(owner, organization.Id);
        await _client.AddProjectMemberAsync(owner, organization.Id, project.Id, await _client.UserIdAsync(member), ProjectRole.Member);
        await ListAsync(member, organization.Id);

        var projects = await factory.Queries.AssertAtMostAsync(1, () => ListAsync(member, organization.Id));
        Assert.Single(projects);
    }

    private async Task<List<ProjectResponse>> ListAsync(TestSession user, Guid organizationId, bool includeArchived = false)
    {
        var response = await _client.SendAsync(user.Authorized(HttpMethod.Get, $"{OrganizationUrl(organizationId)}/projects?includeArchived={includeArchived}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadJsonAsync<List<ProjectResponse>>())!;
    }
}
