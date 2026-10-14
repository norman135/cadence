using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.Projects;
using Cadence.Application.Features.Workflows;
using Cadence.Domain.Issues;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using static Cadence.Api.IntegrationTests.Infrastructure.TestProjects;

namespace Cadence.Api.IntegrationTests.Board;

[Collection(nameof(ApiCollection))]
public sealed class WorkflowTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    private async Task<(TestSession Owner, Guid OrganizationId, Guid ProjectId)> ProjectAsync()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var project = await _client.CreateProjectAsync(owner, organization.Id, "Atlas", "ATL");
        return (owner, organization.Id, project.Id);
    }

    private async Task<int> OpenIssuesAsync(TestSession user, Guid organizationId, Guid projectId)
    {
        var response = await _client.SendAsync(user.Authorized(HttpMethod.Get, ProjectUrl(organizationId, projectId)), TestContext.Current.CancellationToken);
        return (await response.Content.ReadJsonAsync<ProjectResponse>(TestContext.Current.CancellationToken))!.OpenIssueCount;
    }

    [Fact]
    public async Task New_projects_start_with_the_default_workflow_and_issues_start_in_its_default_status()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();

        var workflow = await _client.GetWorkflowAsync(owner, organizationId, projectId);
        var issue = await _client.CreateIssueAsync(owner, organizationId, projectId);
        var detail = await _client.GetIssueAsync(owner, organizationId, issue.Key);

        Assert.Equal(["Backlog", "Todo", "In progress", "In review", "Done", "Canceled"], workflow.Statuses.Select(status => status.Name));
        Assert.False(workflow.RestrictTransitions);
        Assert.Equal(workflow.Statuses.Single(status => status.IsDefault).Id, detail.StatusId);
        Assert.Equal(IssueStatus.Todo, detail.Status);
    }

    [Fact]
    public async Task Admins_reshape_the_workflow_and_issues_follow_their_statuses()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var workflow = await _client.GetWorkflowAsync(owner, organizationId, projectId);
        var (review, canceled) = (workflow.Statuses[3], workflow.Statuses[5]);
        var inReview = await _client.CreateIssueAsync(owner, organizationId, projectId, "Reviewed", new { statusId = review.Id });
        var dropped = await _client.CreateIssueAsync(owner, organizationId, projectId, "Dropped", new { statusId = canceled.Id });
        Assert.Equal(1, await OpenIssuesAsync(owner, organizationId, projectId));

        // Rename "In review" to "Shipped" and count it as done, add "Blocked", and remove "Canceled",
        // sending its issues to "Done" (position 5 in the new list).
        var statuses = workflow.AsInputs();
        statuses[3] = statuses[3] with { Name = "Shipped", Category = IssueStatus.Done };
        statuses.RemoveAt(5);
        statuses.Insert(3, new WorkflowStatusInput(null, "Blocked", IssueStatus.InProgress, 2, false));
        var response = await _client.PutWorkflowAsync(owner, organizationId, projectId, new
        {
            statuses,
            restrictTransitions = false,
            transitions = Array.Empty<object>(),
            replacements = new[] { new { removedStatusId = canceled.Id, replacementIndex = 5 } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadJsonAsync<WorkflowResponse>(TestContext.Current.CancellationToken))!;
        Assert.Equal(["Backlog", "Todo", "In progress", "Blocked", "Shipped", "Done"], updated.Statuses.Select(status => status.Name));
        Assert.Equal(2, updated.Statuses[3].WipLimit);

        var shipped = await _client.GetIssueAsync(owner, organizationId, inReview.Key);
        Assert.Equal((review.Id, IssueStatus.Done), (shipped.StatusId, shipped.Status));
        Assert.NotNull(shipped.CompletedAt);

        var done = await _client.GetIssueAsync(owner, organizationId, dropped.Key);
        Assert.Equal(updated.Statuses[5].Id, done.StatusId);
        Assert.Equal(0, await OpenIssuesAsync(owner, organizationId, projectId));

        // The cached workflow was replaced, so readers see the new one immediately.
        Assert.Equal("Shipped", (await _client.GetWorkflowAsync(owner, organizationId, projectId)).Statuses[4].Name);
    }

    [Fact]
    public async Task Removing_a_status_with_issues_needs_somewhere_to_send_them()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var workflow = await _client.GetWorkflowAsync(owner, organizationId, projectId);
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Waiting", new { statusId = workflow.Statuses[0].Id });
        var statuses = workflow.AsInputs();
        statuses.RemoveAt(0);

        var response = await _client.PutWorkflowAsync(owner, organizationId, projectId, new { statuses, restrictTransitions = false, transitions = Array.Empty<object>() });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "workflows.replacement_required");
        Assert.Equal(6, (await _client.GetWorkflowAsync(owner, organizationId, projectId)).Statuses.Count);
    }

    [Fact]
    public async Task Workflows_are_validated()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var statuses = (await _client.GetWorkflowAsync(owner, organizationId, projectId)).AsInputs();
        statuses.Add(new WorkflowStatusInput(null, "todo", IssueStatus.Todo, null, false));

        var response = await _client.PutWorkflowAsync(owner, organizationId, projectId, new { statuses, restrictTransitions = false, transitions = Array.Empty<object>() });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "workflows.duplicate_status_name");
    }

    [Fact]
    public async Task Only_project_admins_change_the_workflow()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var member = await factory.AddMemberAsync(owner, organizationId, OrganizationRole.Member);
        await _client.AddProjectMemberAsync(owner, organizationId, projectId, await _client.UserIdAsync(member), ProjectRole.Member);
        var outsider = await factory.AddMemberAsync(owner, organizationId, OrganizationRole.Member);
        var body = new
        {
            statuses = (await _client.GetWorkflowAsync(owner, organizationId, projectId)).AsInputs(),
            restrictTransitions = true,
            transitions = Array.Empty<object>(),
        };

        await (await _client.PutWorkflowAsync(member, organizationId, projectId, body)).AssertProblemAsync(HttpStatusCode.Forbidden, "projects.forbidden");
        await (await _client.PutWorkflowAsync(outsider, organizationId, projectId, body)).AssertProblemAsync(HttpStatusCode.NotFound, "projects.not_found");
        Assert.Equal(6, (await _client.GetWorkflowAsync(member, organizationId, projectId)).Statuses.Count);
    }

    [Fact]
    public async Task Restricted_workflows_apply_their_rules_to_status_changes()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var ownerId = await _client.UserIdAsync(owner);
        var workflow = await _client.GetWorkflowAsync(owner, organizationId, projectId);
        var response = await _client.PutWorkflowAsync(owner, organizationId, projectId, new
        {
            statuses = workflow.AsInputs(),
            restrictTransitions = true,
            transitions = new[] { new WorkflowTransitionInput(1, 2, RequireAssignee: true, TransitionActor.Anyone) },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var issue = await _client.CreateIssueAsync(owner, organizationId, projectId);
        var (inProgress, done) = (workflow.Statuses[2].Id, workflow.Statuses[4].Id);

        var skipping = await _client.UpdateIssueAsync(owner, organizationId, issue.Id, new { statusId = done });
        await skipping.AssertProblemAsync(HttpStatusCode.BadRequest, "workflows.transition_not_allowed");

        var unassigned = await _client.UpdateIssueAsync(owner, organizationId, issue.Id, new { statusId = inProgress });
        await unassigned.AssertProblemAsync(HttpStatusCode.BadRequest, "workflows.assignee_required");

        // Assigning in the same update satisfies the rule.
        var started = await _client.UpdateIssueAsync(owner, organizationId, issue.Id, new { statusId = inProgress, assigneeId = ownerId });
        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        Assert.Equal(inProgress, (await _client.GetIssueAsync(owner, organizationId, issue.Key)).StatusId);
    }
}
