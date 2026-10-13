using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.Issues;
using Cadence.Domain.Issues;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using static Cadence.Api.IntegrationTests.Infrastructure.TestProjects;

namespace Cadence.Api.IntegrationTests.Issues;

[Collection(nameof(ApiCollection))]
public sealed class IssueTests(CadenceApiFactory factory)
{
    private static readonly string[] s_clearEstimate = ["Estimate"];

    private readonly HttpClient _client = factory.CreateApiClient();

    private async Task<(TestSession Owner, Guid OrganizationId, Guid ProjectId)> ProjectAsync(string key = "ATL")
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var project = await _client.CreateProjectAsync(owner, organization.Id, "Atlas", key);
        return (owner, organization.Id, project.Id);
    }

    [Fact]
    public async Task Issues_get_readable_keys_numbered_per_project()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var other = await _client.CreateProjectAsync(owner, organizationId, "Nimbus", "NIM");

        Assert.Equal("ATL-1", (await _client.CreateIssueAsync(owner, organizationId, projectId)).Key);
        Assert.Equal("ATL-2", (await _client.CreateIssueAsync(owner, organizationId, projectId)).Key);
        Assert.Equal("NIM-1", (await _client.CreateIssueAsync(owner, organizationId, other.Id)).Key);
    }

    [Fact]
    public async Task Concurrent_creates_never_share_a_number()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();

        var created = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(index => factory.CreateApiClient().CreateIssueAsync(owner, organizationId, projectId, $"Issue {index}")));

        Assert.Equal(Enumerable.Range(1, 20).Select(number => $"ATL-{number}").Order(), created.Select(issue => issue.Key).Order());
    }

    [Fact]
    public async Task An_issue_page_shows_its_fields_labels_and_people()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var ownerId = await _client.UserIdAsync(owner);
        var backend = await _client.CreateLabelAsync(owner, organizationId, "Backend", "Teal");

        var created = await _client.CreateIssueAsync(owner, organizationId, projectId, "Offline sync drops edits", new
        {
            description = "Replays run in parallel.",
            type = "Bug",
            priority = "High",
            assigneeId = ownerId,
            labelIds = new[] { backend.Id },
            estimate = 5,
            dueDate = "2026-10-14",
        });

        var issue = await _client.GetIssueAsync(owner, organizationId, created.Key.ToLowerInvariant());

        Assert.Equal(("ATL-1", IssueType.Bug, IssuePriority.High, IssueStatus.Todo), (issue.Key, issue.Type, issue.Priority, issue.Status));
        Assert.Equal((ownerId, ownerId), (issue.Assignee!.Id, issue.Reporter.Id));
        Assert.Equal((5, new DateOnly(2026, 10, 14)), (issue.Estimate, issue.DueDate));
        Assert.Equal(["Backend"], issue.Labels.Select(label => label.Name));
        Assert.Equal(ProjectRole.Admin, issue.Role);
    }

    [Fact]
    public async Task Updates_change_only_the_given_fields_and_clear_the_listed_ones()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var created = await _client.CreateIssueAsync(owner, organizationId, projectId, "Title", new { estimate = 3, description = "Notes" });

        var updated = await _client.UpdateIssueAsync(owner, organizationId, created.Id, new { status = "Done", clear = s_clearEstimate });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        var issue = await _client.GetIssueAsync(owner, organizationId, created.Key);
        Assert.Equal(IssueStatus.Done, issue.Status);
        Assert.NotNull(issue.CompletedAt);
        Assert.Null(issue.Estimate);
        Assert.Equal(("Title", "Notes"), (issue.Title, issue.Description));
    }

    [Fact]
    public async Task Assignees_must_have_access_to_the_project()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var member = await factory.AddMemberAsync(owner, organizationId, OrganizationRole.Member);

        var response = await _client.CreateIssueResponseAsync(owner, organizationId, new { projectId, title = "Title", assigneeId = await _client.UserIdAsync(member) });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "issues.assignee_not_in_project");
    }

    [Fact]
    public async Task Sub_issues_nest_three_levels_deep_and_parents_count_them()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var epic = await _client.CreateIssueAsync(owner, organizationId, projectId, "Epic", new { type = "Epic" });
        var story = await _client.CreateIssueAsync(owner, organizationId, projectId, "Story", new { parentId = epic.Id });
        var task = await _client.CreateIssueAsync(owner, organizationId, projectId, "Task", new { parentId = story.Id });

        var tooDeep = await _client.CreateIssueResponseAsync(owner, organizationId, new { projectId, title = "Too deep", parentId = task.Id });
        await tooDeep.AssertProblemAsync(HttpStatusCode.BadRequest, "issues.too_deep");

        var cycle = await _client.UpdateIssueAsync(owner, organizationId, story.Id, new { parentId = task.Id });
        await cycle.AssertProblemAsync(HttpStatusCode.BadRequest, "issues.parent_cycle");

        await _client.UpdateIssueAsync(owner, organizationId, task.Id, new { status = "Done" });

        var parent = await _client.GetIssueAsync(owner, organizationId, story.Key);
        Assert.Equal(epic.Id, parent.Parent!.Id);
        Assert.Equal([task.Key], parent.SubIssues.Select(child => child.Key));

        var list = await _client.ListIssuesAsync(owner, organizationId, $"?q={story.Key}");
        Assert.Equal((1, 1), (list.Items[0].SubIssueCount, list.Items[0].SubIssueClosedCount));
    }

    [Fact]
    public async Task Deleting_a_parent_moves_its_sub_issues_to_the_top_level()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var parent = await _client.CreateIssueAsync(owner, organizationId, projectId, "Parent");
        var child = await _client.CreateIssueAsync(owner, organizationId, projectId, "Child", new { parentId = parent.Id });

        var deleted = await _client.SendAsync(owner.Authorized(HttpMethod.Delete, $"{OrganizationUrl(organizationId)}/issues/{parent.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Null((await _client.GetIssueAsync(owner, organizationId, child.Key)).Parent);
    }

    [Fact]
    public async Task Links_read_from_both_sides()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var blocker = await _client.CreateIssueAsync(owner, organizationId, projectId, "Blocker");
        var blocked = await _client.CreateIssueAsync(owner, organizationId, projectId, "Blocked");

        var linked = await _client.SendAsync(owner.Authorized(HttpMethod.Post, $"{OrganizationUrl(organizationId)}/issues/{blocker.Id}/links", new { targetKey = blocked.Key, type = "Blocks" }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);

        var again = await _client.SendAsync(owner.Authorized(HttpMethod.Post, $"{OrganizationUrl(organizationId)}/issues/{blocker.Id}/links", new { targetKey = blocked.Key, type = "Blocks" }), TestContext.Current.CancellationToken);
        await again.AssertProblemAsync(HttpStatusCode.Conflict, "issues.link_exists");

        Assert.Equal([("blocks", blocked.Key)], (await _client.GetIssueAsync(owner, organizationId, blocker.Key)).Links.Select(link => (link.Relation, link.Issue.Key)));
        Assert.Equal([("blocked_by", blocker.Key)], (await _client.GetIssueAsync(owner, organizationId, blocked.Key)).Links.Select(link => (link.Relation, link.Issue.Key)));
    }

    [Fact]
    public async Task Viewers_read_but_cannot_change_and_outsiders_see_nothing()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var viewer = await factory.AddMemberAsync(owner, organizationId, OrganizationRole.Member);
        var outsider = await factory.AddMemberAsync(owner, organizationId, OrganizationRole.Member);
        await _client.AddProjectMemberAsync(owner, organizationId, projectId, await _client.UserIdAsync(viewer), ProjectRole.Viewer);
        var issue = await _client.CreateIssueAsync(owner, organizationId, projectId);

        Assert.Equal(issue.Key, (await _client.GetIssueAsync(viewer, organizationId, issue.Key)).Key);
        var edit = await _client.UpdateIssueAsync(viewer, organizationId, issue.Id, new { title = "Mine now" });
        await edit.AssertProblemAsync(HttpStatusCode.Forbidden, "projects.forbidden");

        var hidden = await _client.SendAsync(outsider.Authorized(HttpMethod.Get, $"{OrganizationUrl(organizationId)}/issues/by-key/{issue.Key}"), TestContext.Current.CancellationToken);
        await hidden.AssertProblemAsync(HttpStatusCode.NotFound, "issues.not_found");
        Assert.Empty((await _client.ListIssuesAsync(outsider, organizationId)).Items);
    }

    [Fact]
    public async Task Another_organization_never_sees_the_issues()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var issue = await _client.CreateIssueAsync(owner, organizationId, projectId);

        var stranger = await factory.SignUpAsync();
        var strangerOrganization = await _client.CreateOrganizationAsync(stranger);

        Assert.Empty((await _client.ListIssuesAsync(stranger, strangerOrganization.Id)).Items);
        var byKey = await _client.SendAsync(stranger.Authorized(HttpMethod.Get, $"{OrganizationUrl(strangerOrganization.Id)}/issues/by-key/{issue.Key}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, byKey.StatusCode);
        var patch = await _client.UpdateIssueAsync(stranger, strangerOrganization.Id, issue.Id, new { title = "Hijacked" });
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    }

    [Fact]
    public async Task The_list_filters_sorts_and_pages_on_the_server()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var ownerId = await _client.UserIdAsync(owner);
        var bug = await _client.CreateLabelAsync(owner, organizationId, $"Bug {Guid.NewGuid():N}"[..20], "Red");

        await _client.CreateIssueAsync(owner, organizationId, projectId, "Low and late", new { priority = "Low", dueDate = "2026-12-01" });
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Urgent crash", new { priority = "Urgent", labelIds = new[] { bug.Id }, assigneeId = ownerId });
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Medium soon", new { priority = "Medium", dueDate = "2026-10-05", status = "InProgress" });
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Finished", new { priority = "High", status = "Done" });

        var byPriority = await _client.ListIssuesAsync(owner, organizationId, "?sort=Priority");
        Assert.Equal(["Urgent crash", "Finished", "Medium soon", "Low and late"], byPriority.Items.Select(issue => issue.Title));

        var byDue = await _client.ListIssuesAsync(owner, organizationId, "?sort=DueDate");
        Assert.Equal(["Medium soon", "Low and late"], byDue.Items.Take(2).Select(issue => issue.Title));

        var open = await _client.ListIssuesAsync(owner, organizationId, "?status=Todo&status=InProgress");
        Assert.Equal(3, open.Items.Count);

        Assert.Equal(["Urgent crash"], (await _client.ListIssuesAsync(owner, organizationId, $"?labelId={bug.Id}")).Items.Select(issue => issue.Title));
        Assert.Equal(["Urgent crash"], (await _client.ListIssuesAsync(owner, organizationId, $"?assigneeId={ownerId}")).Items.Select(issue => issue.Title));
        Assert.Equal(3, (await _client.ListIssuesAsync(owner, organizationId, "?unassigned=true")).Items.Count);
        Assert.Equal(["Medium soon"], (await _client.ListIssuesAsync(owner, organizationId, "?q=SOON")).Items.Select(issue => issue.Title));
    }

    [Theory]
    [InlineData("Updated")]
    [InlineData("Created")]
    [InlineData("Priority")]
    [InlineData("DueDate")]
    public async Task Keyset_pages_cover_every_issue_once_in_order(string sort)
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        string[] priorities = ["None", "Low", "Medium", "High", "Urgent"];
        for (var index = 0; index < 11; index++)
        {
            await _client.CreateIssueAsync(owner, organizationId, projectId, $"Issue {index}", new
            {
                priority = priorities[index % priorities.Length],
                dueDate = index % 3 == 0 ? null : $"2026-11-{10 + (index % 4):00}",
            });
        }

        var all = await _client.ListIssuesAsync(owner, organizationId, $"?sort={sort}&limit=100");

        var paged = new List<IssueListItem>();
        string? cursor = null;
        do
        {
            var page = await _client.ListIssuesAsync(owner, organizationId, $"?sort={sort}&limit=4{(cursor is null ? string.Empty : $"&after={cursor}")}");
            paged.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(11, all.Items.Count);
        Assert.Equal(all.Items.Select(issue => issue.Id), paged.Select(issue => issue.Id));
    }

    [Fact]
    public async Task Listing_issues_stays_within_two_queries()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var member = await factory.AddMemberAsync(owner, organizationId, OrganizationRole.Member);
        await _client.AddProjectMemberAsync(owner, organizationId, projectId, await _client.UserIdAsync(member), ProjectRole.Member);
        var label = await _client.CreateLabelAsync(owner, organizationId, $"L{Guid.NewGuid():N}"[..12]);
        for (var index = 0; index < 5; index++)
        {
            await _client.CreateIssueAsync(owner, organizationId, projectId, $"Issue {index}", new { labelIds = new[] { label.Id } });
        }

        // Warm the membership and project access caches, as any earlier request would.
        await _client.ListIssuesAsync(member, organizationId);

        var page = await factory.Queries.AssertAtMostAsync(2, () => _client.ListIssuesAsync(member, organizationId, "?sort=Priority&status=Todo"));
        Assert.Equal(5, page.Items.Count);
        Assert.All(page.Items, issue => Assert.Single(issue.Labels));
    }

    [Fact]
    public async Task Project_open_issue_counts_follow_status_changes()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var first = await _client.CreateIssueAsync(owner, organizationId, projectId);
        await _client.CreateIssueAsync(owner, organizationId, projectId);

        await _client.UpdateIssueAsync(owner, organizationId, first.Id, new { status = "Canceled" });

        var project = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"{OrganizationUrl(organizationId)}/projects/{projectId}"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<Application.Features.Projects.ProjectResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(1, project!.OpenIssueCount);
    }
}
