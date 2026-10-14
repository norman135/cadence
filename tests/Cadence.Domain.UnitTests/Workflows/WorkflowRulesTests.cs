using Cadence.Domain.Issues;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;

namespace Cadence.Domain.UnitTests.Workflows;

public sealed class WorkflowRulesTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_user = Guid.CreateVersion7();
    private static readonly Guid s_other = Guid.CreateVersion7();

    private static Workflow NewWorkflow() =>
        Workflow.CreateDefault(Project.Create(Guid.CreateVersion7(), "ATL", "Atlas", null, s_user, s_now).Value);

    private static List<StatusDraft> Drafts(Workflow workflow) =>
        [.. workflow.Statuses.Select(status => new StatusDraft(status.Id, status.Name, status.Category, status.WipLimit, status.IsDefault))];

    [Fact]
    public void The_default_template_has_one_status_per_category_and_starts_in_todo()
    {
        var workflow = NewWorkflow();

        Assert.Equal(
            [IssueStatus.Backlog, IssueStatus.Todo, IssueStatus.InProgress, IssueStatus.InReview, IssueStatus.Done, IssueStatus.Canceled],
            workflow.Statuses.Select(status => status.Category));
        Assert.Equal("Todo", workflow.DefaultStatus.Name);
        Assert.False(workflow.RestrictTransitions);
    }

    [Fact]
    public void Redefining_keeps_updates_adds_and_removes_statuses()
    {
        var workflow = NewWorkflow();
        var drafts = Drafts(workflow);
        var canceled = drafts[5];
        var review = drafts[3];
        drafts.RemoveAt(5);
        drafts[3] = review with { Name = "QA", Category = IssueStatus.InProgress, WipLimit = 3 };
        drafts.Insert(3, new StatusDraft(null, "Blocked", IssueStatus.InProgress, null, false));

        var changes = workflow.Redefine(drafts, restrictTransitions: false, []).Value;

        Assert.Equal(["Backlog", "Todo", "In progress", "Blocked", "QA", "Done"], workflow.Statuses.Select(status => status.Name));
        Assert.Equal([canceled.Id!.Value], changes.RemovedStatusIds);
        Assert.Equal([review.Id!.Value], changes.RecategorizedStatusIds);
        Assert.Equal(3, workflow.FindStatus(review.Id.Value)!.WipLimit);
    }

    [Fact]
    public void Status_names_are_unique_ignoring_case()
    {
        var drafts = Drafts(NewWorkflow());
        drafts.Add(new StatusDraft(null, "todo", IssueStatus.Todo, null, false));

        Assert.Equal("workflows.duplicate_status_name", NewWorkflow().Redefine(drafts, false, []).Error.Code);
    }

    [Fact]
    public void Exactly_one_open_status_is_the_default()
    {
        var workflow = NewWorkflow();
        var drafts = Drafts(workflow);
        drafts[1] = drafts[1] with { IsDefault = false };
        Assert.Equal("workflows.default_status", workflow.Redefine(drafts, false, []).Error.Code);

        drafts[4] = drafts[4] with { IsDefault = true };
        Assert.Equal("workflows.default_status", workflow.Redefine(drafts, false, []).Error.Code);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(999, true)]
    [InlineData(1000, false)]
    public void Wip_limits_are_between_one_and_999(int limit, bool valid)
    {
        var workflow = NewWorkflow();
        var drafts = Drafts(workflow);
        drafts[2] = drafts[2] with { WipLimit = limit };

        Assert.Equal(valid, workflow.Redefine(drafts, false, []).IsSuccess);
    }

    [Fact]
    public void Transitions_connect_two_different_statuses()
    {
        var workflow = NewWorkflow();
        var drafts = Drafts(workflow);

        Assert.Equal("workflows.invalid_transition", workflow.Redefine(drafts, true, [new TransitionDraft(1, 1, false, TransitionActor.Anyone)]).Error.Code);
        Assert.Equal("workflows.invalid_transition", workflow.Redefine(drafts, true, [new TransitionDraft(1, 9, false, TransitionActor.Anyone)]).Error.Code);
    }

    [Fact]
    public void Unrestricted_workflows_allow_any_move()
    {
        var workflow = NewWorkflow();
        var statuses = workflow.Statuses;

        Assert.True(workflow.CanMove(statuses[0].Id, statuses[4].Id, null, s_user, ProjectRole.Member).IsSuccess);
    }

    [Fact]
    public void Restricted_workflows_allow_only_listed_moves_and_apply_their_rules()
    {
        var workflow = NewWorkflow();
        var drafts = Drafts(workflow);
        var redefined = workflow.Redefine(
            drafts,
            restrictTransitions: true,
            [
                new TransitionDraft(1, 2, RequireAssignee: true, TransitionActor.Anyone),
                new TransitionDraft(2, 4, RequireAssignee: false, TransitionActor.AssigneeOrAdmins),
                new TransitionDraft(4, 1, RequireAssignee: false, TransitionActor.Admins),
            ]);
        Assert.True(redefined.IsSuccess);
        var (todo, progress, done) = (workflow.Statuses[1].Id, workflow.Statuses[2].Id, workflow.Statuses[4].Id);

        Assert.Equal("workflows.transition_not_allowed", workflow.CanMove(todo, done, s_user, s_user, ProjectRole.Admin).Error.Code);
        Assert.Equal("workflows.assignee_required", workflow.CanMove(todo, progress, null, s_user, ProjectRole.Member).Error.Code);
        Assert.True(workflow.CanMove(todo, progress, s_other, s_user, ProjectRole.Member).IsSuccess);

        Assert.Equal("workflows.transition_restricted", workflow.CanMove(progress, done, s_other, s_user, ProjectRole.Member).Error.Code);
        Assert.True(workflow.CanMove(progress, done, s_user, s_user, ProjectRole.Member).IsSuccess);
        Assert.True(workflow.CanMove(progress, done, s_other, s_user, ProjectRole.Admin).IsSuccess);

        Assert.Equal("workflows.transition_restricted", workflow.CanMove(done, todo, s_user, s_user, ProjectRole.Member).Error.Code);

        // Reordering inside a column is never a transition.
        Assert.True(workflow.CanMove(done, done, null, s_user, ProjectRole.Member).IsSuccess);
        Assert.True(workflow.MoveDependsOnAssignee(todo, progress));
        Assert.False(workflow.MoveDependsOnAssignee(done, todo));
    }
}
