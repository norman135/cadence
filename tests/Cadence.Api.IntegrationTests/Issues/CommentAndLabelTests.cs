using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Comments;
using Cadence.Application.Features.Labels;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using static Cadence.Api.IntegrationTests.Infrastructure.TestProjects;

namespace Cadence.Api.IntegrationTests.Issues;

[Collection(nameof(ApiCollection))]
public sealed class CommentAndLabelTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Comments_keep_their_edit_history_and_count_on_the_issue()
    {
        var owner = await factory.SignUpAsync(displayName: "Ada Lovelace");
        var organization = await _client.CreateOrganizationAsync(owner);
        var project = await _client.CreateProjectAsync(owner, organization.Id);
        var issue = await _client.CreateIssueAsync(owner, organization.Id, project.Id);
        var commentsUrl = $"{OrganizationUrl(organization.Id)}/issues/{issue.Id}/comments";

        var created = await _client.SendAsync(owner.Authorized(HttpMethod.Post, commentsUrl, new { body = "First draft" }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var comment = await created.Content.ReadJsonAsync<CommentResponse>(TestContext.Current.CancellationToken);

        var edited = await _client.SendAsync(owner.Authorized(HttpMethod.Patch, $"{commentsUrl}/{comment!.Id}", new { body = "Second draft" }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);

        var page = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, commentsUrl), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<KeysetPage<CommentResponse>>(TestContext.Current.CancellationToken);
        var listed = Assert.Single(page!.Items);
        Assert.Equal(("Second draft", "Ada Lovelace", 1), (listed.Body, listed.Author.DisplayName, listed.RevisionCount));
        Assert.NotNull(listed.EditedAt);

        var revisions = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"{commentsUrl}/{comment.Id}/revisions"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<List<CommentRevisionResponse>>(TestContext.Current.CancellationToken);
        Assert.Equal(["First draft"], revisions!.Select(revision => revision.Body));

        Assert.Equal(1, (await _client.GetIssueAsync(owner, organization.Id, issue.Key)).CommentCount);
    }

    [Fact]
    public async Task Only_authors_edit_and_project_admins_may_also_delete()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var member = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Member);
        var project = await _client.CreateProjectAsync(owner, organization.Id);
        await _client.AddProjectMemberAsync(owner, organization.Id, project.Id, await _client.UserIdAsync(member), ProjectRole.Member);
        var issue = await _client.CreateIssueAsync(owner, organization.Id, project.Id);
        var commentsUrl = $"{OrganizationUrl(organization.Id)}/issues/{issue.Id}/comments";

        var comment = await (await _client.SendAsync(member.Authorized(HttpMethod.Post, commentsUrl, new { body = "Mine" }), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<CommentResponse>(TestContext.Current.CancellationToken);

        var ownerEdits = await _client.SendAsync(owner.Authorized(HttpMethod.Patch, $"{commentsUrl}/{comment!.Id}", new { body = "Not yours" }), TestContext.Current.CancellationToken);
        await ownerEdits.AssertProblemAsync(HttpStatusCode.Forbidden, "comments.not_author");

        var ownerDeletes = await _client.SendAsync(owner.Authorized(HttpMethod.Delete, $"{commentsUrl}/{comment.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, ownerDeletes.StatusCode);
        Assert.Equal(0, (await _client.GetIssueAsync(owner, organization.Id, issue.Key)).CommentCount);
    }

    [Fact]
    public async Task Label_names_are_unique_ignoring_case_and_deleting_one_unlabels_issues()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var project = await _client.CreateProjectAsync(owner, organization.Id);
        var label = await _client.CreateLabelAsync(owner, organization.Id, "Backend");

        var duplicate = await _client.SendAsync(owner.Authorized(HttpMethod.Post, $"{OrganizationUrl(organization.Id)}/labels", new { name = "backend", color = "Blue" }), TestContext.Current.CancellationToken);
        await duplicate.AssertProblemAsync(HttpStatusCode.Conflict, "labels.name_taken");

        var issue = await _client.CreateIssueAsync(owner, organization.Id, project.Id, "Labelled", new { labelIds = new[] { label.Id } });
        var deleted = await _client.SendAsync(owner.Authorized(HttpMethod.Delete, $"{OrganizationUrl(organization.Id)}/labels/{label.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Empty((await _client.GetIssueAsync(owner, organization.Id, issue.Key)).Labels);
        var labels = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, $"{OrganizationUrl(organization.Id)}/labels"), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<List<LabelResponse>>(TestContext.Current.CancellationToken);
        Assert.Empty(labels!);
    }

    [Fact]
    public async Task Guests_cannot_manage_labels_or_create_projects()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var guest = await factory.AddMemberAsync(owner, organization.Id, OrganizationRole.Guest);

        var label = await _client.SendAsync(guest.Authorized(HttpMethod.Post, $"{OrganizationUrl(organization.Id)}/labels", new { name = "Mine", color = "Teal" }), TestContext.Current.CancellationToken);
        var project = await _client.SendAsync(guest.Authorized(HttpMethod.Post, $"{OrganizationUrl(organization.Id)}/projects", new { name = "Mine" }), TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (label.StatusCode, project.StatusCode));
    }
}
