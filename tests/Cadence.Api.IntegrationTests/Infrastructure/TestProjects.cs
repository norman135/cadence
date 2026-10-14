using System.Net;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Board;
using Cadence.Application.Features.Issues;
using Cadence.Application.Features.Labels;
using Cadence.Application.Features.Projects;
using Cadence.Application.Features.Workflows;
using Cadence.Domain.Projects;

namespace Cadence.Api.IntegrationTests.Infrastructure;

/// <summary>Drives the real project, label and issue endpoints to set up test scenarios.</summary>
public static class TestProjects
{
    public static string OrganizationUrl(Guid organizationId) => $"/api/v1/organizations/{organizationId}";

    public static async Task<ProjectResponse> CreateProjectAsync(
        this HttpClient client, TestSession user, Guid organizationId, string name = "Atlas Mobile", string? key = null)
    {
        var response = await client.SendAsync(user.Authorized(HttpMethod.Post, $"{OrganizationUrl(organizationId)}/projects", new { name, key }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadJsonAsync<ProjectResponse>())!;
    }

    public static async Task AddProjectMemberAsync(
        this HttpClient client, TestSession admin, Guid organizationId, Guid projectId, Guid userId, ProjectRole role)
    {
        var response = await client.SendAsync(admin.Authorized(
            HttpMethod.Post,
            $"{OrganizationUrl(organizationId)}/projects/{projectId}/members",
            new { userId, role = role.ToString() }));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static Task<HttpResponseMessage> CreateIssueResponseAsync(this HttpClient client, TestSession user, Guid organizationId, object body) =>
        client.SendAsync(user.Authorized(HttpMethod.Post, $"{OrganizationUrl(organizationId)}/issues", body));

    public static async Task<CreatedIssueResponse> CreateIssueAsync(
        this HttpClient client, TestSession user, Guid organizationId, Guid projectId, string title = "Offline sync drops edits", object? fields = null)
    {
        var body = new Dictionary<string, object?> { ["projectId"] = projectId, ["title"] = title };
        if (fields is not null)
        {
            foreach (var property in fields.GetType().GetProperties())
            {
                body[char.ToLowerInvariant(property.Name[0]) + property.Name[1..]] = property.GetValue(fields);
            }
        }

        var response = await client.CreateIssueResponseAsync(user, organizationId, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadJsonAsync<CreatedIssueResponse>())!;
    }

    public static Task<HttpResponseMessage> UpdateIssueAsync(this HttpClient client, TestSession user, Guid organizationId, Guid issueId, object body) =>
        client.SendAsync(user.Authorized(HttpMethod.Patch, $"{OrganizationUrl(organizationId)}/issues/{issueId}", body));

    public static async Task<IssueDetail> GetIssueAsync(this HttpClient client, TestSession user, Guid organizationId, string key)
    {
        var response = await client.SendAsync(user.Authorized(HttpMethod.Get, $"{OrganizationUrl(organizationId)}/issues/by-key/{key}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadJsonAsync<IssueDetail>())!;
    }

    public static async Task<KeysetPage<IssueListItem>> ListIssuesAsync(this HttpClient client, TestSession user, Guid organizationId, string query = "")
    {
        var response = await client.SendAsync(user.Authorized(HttpMethod.Get, $"{OrganizationUrl(organizationId)}/issues{query}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadJsonAsync<KeysetPage<IssueListItem>>())!;
    }

    public static async Task<LabelResponse> CreateLabelAsync(this HttpClient client, TestSession user, Guid organizationId, string name, string color = "Teal")
    {
        var response = await client.SendAsync(user.Authorized(HttpMethod.Post, $"{OrganizationUrl(organizationId)}/labels", new { name, color }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadJsonAsync<LabelResponse>())!;
    }

    /// <summary>The id of a signed-in user, from /me.</summary>
    public static async Task<Guid> UserIdAsync(this HttpClient client, TestSession user)
    {
        var response = await client.SendAsync(user.Authorized(HttpMethod.Get, "/api/v1/me"));
        var me = await response.Content.ReadJsonAsync<Application.Features.Me.CurrentUserResponse>();
        return me!.Id;
    }

    public static string ProjectUrl(Guid organizationId, Guid projectId) => $"{OrganizationUrl(organizationId)}/projects/{projectId}";

    public static async Task<WorkflowResponse> GetWorkflowAsync(this HttpClient client, TestSession user, Guid organizationId, Guid projectId)
    {
        var response = await client.SendAsync(user.Authorized(HttpMethod.Get, $"{ProjectUrl(organizationId, projectId)}/workflow"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadJsonAsync<WorkflowResponse>())!;
    }

    public static Task<HttpResponseMessage> PutWorkflowAsync(this HttpClient client, TestSession user, Guid organizationId, Guid projectId, object body) =>
        client.SendAsync(user.Authorized(HttpMethod.Put, $"{ProjectUrl(organizationId, projectId)}/workflow", body));

    /// <summary>The workflow's statuses as the editor would resubmit them unchanged.</summary>
    public static List<WorkflowStatusInput> AsInputs(this WorkflowResponse workflow) =>
        [.. workflow.Statuses.Select(status => new WorkflowStatusInput(status.Id, status.Name, status.Category, status.WipLimit, status.IsDefault))];

    public static async Task<BoardResponse> GetBoardAsync(this HttpClient client, TestSession user, Guid organizationId, Guid projectId, string query = "")
    {
        var response = await client.SendAsync(user.Authorized(HttpMethod.Get, $"{ProjectUrl(organizationId, projectId)}/board{query}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadJsonAsync<BoardResponse>())!;
    }

    public static Task<HttpResponseMessage> MoveCardAsync(
        this HttpClient client, TestSession user, Guid organizationId, Guid projectId, BoardCard card, Guid toStatusId, string rank) =>
        client.SendAsync(user.Authorized(
            HttpMethod.Post,
            $"{ProjectUrl(organizationId, projectId)}/board/moves",
            new { issueId = card.Id, fromStatusId = card.StatusId, toStatusId, rank, version = card.Version, assigneeId = card.Assignee?.Id }));
}
