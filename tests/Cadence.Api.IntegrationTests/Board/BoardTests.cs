using System.Net;
using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.Board;
using Cadence.Application.Features.Projects;
using Cadence.Application.Features.Workflows;
using Cadence.Domain.Issues;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Cadence.Api.IntegrationTests.Infrastructure.TestProjects;

namespace Cadence.Api.IntegrationTests.Board;

[Collection(nameof(ApiCollection))]
public sealed class BoardTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    private async Task<(TestSession Owner, Guid OrganizationId, Guid ProjectId)> ProjectAsync()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var project = await _client.CreateProjectAsync(owner, organization.Id, "Atlas", "ATL");
        return (owner, organization.Id, project.Id);
    }

    private static BoardColumn Column(BoardResponse board, string name) => board.Columns.Single(column => column.Name == name);

    private async Task SqlAsync(FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CadenceDbContext>().Database.ExecuteSqlAsync(sql, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_board_has_a_column_per_status_with_cards_in_rank_order_in_one_query()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var epic = await _client.CreateIssueAsync(owner, organizationId, projectId, "Offline mode", new { type = "Epic", statusId = await factory.StatusIdAsync(projectId, IssueStatus.Backlog) });
        var first = await _client.CreateIssueAsync(owner, organizationId, projectId, "First", new { parentId = epic.Id });
        var second = await _client.CreateIssueAsync(owner, organizationId, projectId, "Second");
        await _client.GetBoardAsync(owner, organizationId, projectId);

        var board = await factory.Queries.AssertAtMostAsync(1, () => _client.GetBoardAsync(owner, organizationId, projectId));

        Assert.Equal("ATL", board.ProjectKey);
        Assert.Equal(["Backlog", "Todo", "In progress", "In review", "Done", "Canceled"], board.Columns.Select(column => column.Name));
        var todo = Column(board, "Todo");
        Assert.Equal([first.Key, second.Key], todo.Cards.Select(card => card.Key));
        Assert.True(string.CompareOrdinal(todo.Cards[0].Rank, todo.Cards[1].Rank) < 0);
        Assert.Equal((2, false, null), (todo.Total, todo.HasMore, todo.NextRank));
        Assert.Equal(epic.Key, todo.Cards[0].Epic?.Key);
        Assert.Null(todo.Cards[1].Epic);
    }

    [Fact]
    public async Task Long_columns_show_their_first_cards_and_where_the_rest_begin()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        for (var i = 0; i <= GetBoardQueryHandler.ColumnLimit; i++)
        {
            await _client.CreateIssueAsync(owner, organizationId, projectId, $"Issue {i}");
        }

        var todo = Column(await _client.GetBoardAsync(owner, organizationId, projectId), "Todo");

        Assert.Equal(GetBoardQueryHandler.ColumnLimit, todo.Cards.Count);
        Assert.Equal(GetBoardQueryHandler.ColumnLimit + 1, todo.Total);
        Assert.True(todo.HasMore);
        Assert.True(string.CompareOrdinal(todo.Cards[^1].Rank, todo.NextRank) < 0);
    }

    [Fact]
    public async Task Done_columns_show_recently_closed_issues_only()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var done = await factory.StatusIdAsync(projectId, IssueStatus.Done);
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Fresh", new { statusId = done });
        var old = await _client.CreateIssueAsync(owner, organizationId, projectId, "Old", new { statusId = done });
        await SqlAsync($"UPDATE issues SET completed_at = now() - interval '30 days' WHERE id = {old.Id}");

        var column = Column(await _client.GetBoardAsync(owner, organizationId, projectId), "Done");

        Assert.Equal(["Fresh"], column.Cards.Select(card => card.Title));
        Assert.Equal(1, column.Total);
    }

    [Fact]
    public async Task Quick_filters_narrow_the_cards_but_not_the_column_totals()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var ownerId = await _client.UserIdAsync(owner);
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Mine", new { assigneeId = ownerId, type = "Bug" });
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Nobody's");

        var mine = Column(await _client.GetBoardAsync(owner, organizationId, projectId, $"?assigneeId={ownerId}"), "Todo");
        var bugs = Column(await _client.GetBoardAsync(owner, organizationId, projectId, "?type=Bug"), "Todo");
        var unassigned = Column(await _client.GetBoardAsync(owner, organizationId, projectId, "?unassigned=true"), "Todo");

        Assert.Equal(["Mine"], mine.Cards.Select(card => card.Title));
        Assert.Equal(["Mine"], bugs.Cards.Select(card => card.Title));
        Assert.Equal(["Nobody's"], unassigned.Cards.Select(card => card.Title));
        Assert.Equal(2, mine.Total);
    }

    [Fact]
    public async Task Moving_a_card_is_one_statement_and_returns_its_new_version()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        await _client.CreateIssueAsync(owner, organizationId, projectId, "A");
        await _client.CreateIssueAsync(owner, organizationId, projectId, "B");
        var board = await _client.GetBoardAsync(owner, organizationId, projectId);
        var (todo, inProgress) = (Column(board, "Todo"), Column(board, "In progress"));
        var card = todo.Cards[1];

        // Warm the caches with a move that changes nothing, then measure a real move.
        var warm = await _client.MoveCardAsync(owner, organizationId, projectId, card, card.StatusId, card.Rank);
        card = card with { Version = (await warm.Content.ReadJsonAsync<MovedCardResponse>(TestContext.Current.CancellationToken))!.Version };
        var rank = Rank.Between(null, null);
        var moved = await factory.Queries.AssertAtMostAsync(1, () => _client.MoveCardAsync(owner, organizationId, projectId, card, inProgress.StatusId, rank));

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var result = (await moved.Content.ReadJsonAsync<MovedCardResponse>(TestContext.Current.CancellationToken))!;
        Assert.Equal((inProgress.StatusId, IssueStatus.InProgress, rank), (result.StatusId, result.Category, result.Rank));
        Assert.NotEqual(card.Version, result.Version);

        var after = await _client.GetBoardAsync(owner, organizationId, projectId);
        Assert.Equal(["B"], Column(after, "In progress").Cards.Select(c => c.Title));
        Assert.Equal(result.Version, Column(after, "In progress").Cards[0].Version);
    }

    [Fact]
    public async Task A_move_based_on_an_old_version_is_refused_and_changes_nothing()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var issue = await _client.CreateIssueAsync(owner, organizationId, projectId, "Contended");
        var board = await _client.GetBoardAsync(owner, organizationId, projectId);
        var card = Column(board, "Todo").Cards[0];
        var (inProgress, done) = (Column(board, "In progress").StatusId, Column(board, "Done").StatusId);

        var first = await _client.MoveCardAsync(owner, organizationId, projectId, card, inProgress, card.Rank);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The second client still holds the version from before the first move.
        var stale = await _client.MoveCardAsync(owner, organizationId, projectId, card with { StatusId = inProgress }, done, card.Rank);

        await stale.AssertProblemAsync(HttpStatusCode.Conflict, "issues.changed");
        Assert.Equal(IssueStatus.InProgress, (await _client.GetIssueAsync(owner, organizationId, issue.Key)).Status);
    }

    [Fact]
    public async Task Closing_a_card_by_moving_it_updates_counts_and_completion()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var parent = await _client.CreateIssueAsync(owner, organizationId, projectId, "Parent");
        var child = await _client.CreateIssueAsync(owner, organizationId, projectId, "Child", new { parentId = parent.Id });
        var board = await _client.GetBoardAsync(owner, organizationId, projectId);
        var card = Column(board, "Todo").Cards.Single(c => c.Id == child.Id);

        var moved = await _client.MoveCardAsync(owner, organizationId, projectId, card, Column(board, "Done").StatusId, card.Rank);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var closed = await _client.GetIssueAsync(owner, organizationId, child.Key);
        Assert.NotNull(closed.CompletedAt);
        Assert.Equal(1, (await _client.GetIssueAsync(owner, organizationId, parent.Key)).SubIssues.Count(sub => sub.Status == IssueStatus.Done));
        var project = await (await _client.SendAsync(owner.Authorized(HttpMethod.Get, ProjectUrl(organizationId, projectId)), TestContext.Current.CancellationToken))
            .Content.ReadJsonAsync<ProjectResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(1, project!.OpenIssueCount);
    }

    [Fact]
    public async Task Moves_follow_the_workflow_rules_and_project_roles()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        var workflow = await _client.GetWorkflowAsync(owner, organizationId, projectId);
        var response = await _client.PutWorkflowAsync(owner, organizationId, projectId, new
        {
            statuses = workflow.AsInputs(),
            restrictTransitions = true,
            transitions = new[]
            {
                new WorkflowTransitionInput(1, 2, RequireAssignee: false, TransitionActor.AssigneeOrAdmins),
                new WorkflowTransitionInput(1, 4, RequireAssignee: false, TransitionActor.Admins),
            },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var member = await factory.AddMemberAsync(owner, organizationId, OrganizationRole.Member);
        var memberId = await _client.UserIdAsync(member);
        await _client.AddProjectMemberAsync(owner, organizationId, projectId, memberId, ProjectRole.Member);
        var viewer = await factory.AddMemberAsync(owner, organizationId, OrganizationRole.Member);
        await _client.AddProjectMemberAsync(owner, organizationId, projectId, await _client.UserIdAsync(viewer), ProjectRole.Viewer);
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Someone else's", new { assigneeId = await _client.UserIdAsync(owner) });
        await _client.CreateIssueAsync(owner, organizationId, projectId, "Mine", new { assigneeId = memberId });
        var cards = Column(await _client.GetBoardAsync(member, organizationId, projectId), "Todo").Cards;
        var (others, mine) = (cards[0], cards[1]);
        var (inProgress, inReview, done) = (workflow.Statuses[2].Id, workflow.Statuses[3].Id, workflow.Statuses[4].Id);

        await (await _client.MoveCardAsync(member, organizationId, projectId, mine, inReview, mine.Rank))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "workflows.transition_not_allowed");
        await (await _client.MoveCardAsync(member, organizationId, projectId, mine, done, mine.Rank))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "workflows.transition_restricted");
        await (await _client.MoveCardAsync(member, organizationId, projectId, others, inProgress, others.Rank))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "workflows.transition_restricted");
        await (await _client.MoveCardAsync(viewer, organizationId, projectId, mine, inProgress, mine.Rank))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "projects.forbidden");

        // A client that claims to be the assignee when it isn't is caught by the UPDATE itself.
        await (await _client.MoveCardAsync(member, organizationId, projectId, others with { Assignee = mine.Assignee }, inProgress, others.Rank))
            .AssertProblemAsync(HttpStatusCode.Conflict, "issues.changed");

        Assert.Equal(HttpStatusCode.OK, (await _client.MoveCardAsync(member, organizationId, projectId, mine, inProgress, mine.Rank)).StatusCode);
    }

    [Fact]
    public async Task Archived_projects_take_no_moves()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        await _client.CreateIssueAsync(owner, organizationId, projectId);
        var board = await _client.GetBoardAsync(owner, organizationId, projectId);
        var card = Column(board, "Todo").Cards[0];
        await _client.SendAsync(owner.Authorized(HttpMethod.Post, $"{ProjectUrl(organizationId, projectId)}/archive"), TestContext.Current.CancellationToken);

        var moved = await _client.MoveCardAsync(owner, organizationId, projectId, card, Column(board, "Done").StatusId, card.Rank);

        await moved.AssertProblemAsync(HttpStatusCode.Conflict, "projects.archived");
    }
}
