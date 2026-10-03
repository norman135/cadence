using Cadence.Domain.Issues;
using Cadence.Domain.Projects;

namespace Cadence.Domain.UnitTests.Issues;

public sealed class IssueRulesTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_user = Guid.CreateVersion7();
    private static readonly Project s_project = Project.Create(Guid.CreateVersion7(), "ATL", "Atlas", null, s_user, s_now).Value;

    private static Issue NewIssue(IssueType type = IssueType.Task, Project? project = null, IssueStatus status = IssueStatus.Todo) =>
        Issue.Create(project ?? s_project, 1, type, "Offline sync drops edits", null, status, IssuePriority.Medium, s_user, s_now).Value;

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("x", true)]
    public void Titles_are_required(string title, bool valid) =>
        Assert.Equal(valid, Issue.Create(s_project, 1, IssueType.Task, title, null, IssueStatus.Todo, IssuePriority.None, s_user, s_now).IsSuccess);

    [Fact]
    public void Titles_are_at_most_two_hundred_characters()
    {
        var issue = NewIssue();
        Assert.True(issue.Retitle(new string('x', Issue.MaxTitleLength), s_now).IsSuccess);
        Assert.Equal("issues.invalid_title", issue.Retitle(new string('x', Issue.MaxTitleLength + 1), s_now).Error.Code);
    }

    [Fact]
    public void Archived_projects_take_no_new_issues()
    {
        var archived = Project.Create(Guid.CreateVersion7(), "OLD", "Old", null, s_user, s_now).Value;
        archived.Archive(s_now);

        var result = Issue.Create(archived, 1, IssueType.Task, "Title", null, IssueStatus.Todo, IssuePriority.None, s_user, s_now);

        Assert.Equal("projects.archived", result.Error.Code);
    }

    [Fact]
    public void Closing_records_when_and_reopening_clears_it()
    {
        var issue = NewIssue();
        var later = s_now.AddHours(3);

        issue.MoveTo(IssueStatus.Done, later);
        Assert.Equal(later, issue.CompletedAt);

        // Moving between closed states keeps the original completion time.
        issue.MoveTo(IssueStatus.Canceled, later.AddHours(1));
        Assert.Equal(later, issue.CompletedAt);

        issue.MoveTo(IssueStatus.InProgress, later.AddHours(2));
        Assert.Null(issue.CompletedAt);
        Assert.Equal(later.AddHours(2), issue.UpdatedAt);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Estimates_are_zero_to_one_hundred_points(int points, bool valid) =>
        Assert.Equal(valid, NewIssue().EstimateAt(points, s_now).IsSuccess);

    [Fact]
    public void Issues_nest_under_a_parent_in_the_same_project()
    {
        var epic = NewIssue(IssueType.Epic);
        var story = NewIssue(IssueType.Story);

        Assert.True(story.MoveUnder(epic, [epic.Id], subtreeHeight: 0, s_now).IsSuccess);
        Assert.Equal(epic.Id, story.ParentId);

        var otherProject = Project.Create(Guid.CreateVersion7(), "NIM", "Nimbus", null, s_user, s_now).Value;
        var stranger = NewIssue(project: otherProject);
        Assert.Equal("issues.parent_in_other_project", NewIssue().MoveUnder(stranger, [stranger.Id], 0, s_now).Error.Code);
    }

    [Fact]
    public void Epics_stay_top_level()
    {
        var parent = NewIssue();
        var epic = NewIssue(IssueType.Epic);
        Assert.Equal("issues.epic_cannot_have_parent", epic.MoveUnder(parent, [parent.Id], 0, s_now).Error.Code);

        var child = NewIssue();
        child.MoveUnder(parent, [parent.Id], 0, s_now);
        Assert.Equal("issues.epic_cannot_have_parent", child.ChangeType(IssueType.Epic, s_now).Error.Code);
    }

    [Fact]
    public void Nesting_stops_at_three_levels_including_existing_sub_issues()
    {
        var level0 = NewIssue(IssueType.Epic);
        var level1 = NewIssue();
        var level2 = NewIssue();

        // A third level under level 2 would be a fourth level.
        Assert.Equal("issues.too_deep", NewIssue().MoveUnder(level2, [level2.Id, level1.Id, level0.Id], 0, s_now).Error.Code);

        // An issue that already has sub-issues can only go where they still fit.
        Assert.True(NewIssue().MoveUnder(level0, [level0.Id], subtreeHeight: 1, s_now).IsSuccess);
        Assert.Equal("issues.too_deep", NewIssue().MoveUnder(level1, [level1.Id, level0.Id], subtreeHeight: 1, s_now).Error.Code);
    }

    [Fact]
    public void An_issue_cannot_go_under_itself_or_its_own_sub_issues()
    {
        var parent = NewIssue();
        var child = NewIssue();

        Assert.Equal("issues.parent_cycle", parent.MoveUnder(parent, [parent.Id], 0, s_now).Error.Code);
        Assert.Equal("issues.parent_cycle", parent.MoveUnder(child, [child.Id, parent.Id], 1, s_now).Error.Code);
    }

    [Fact]
    public void Moving_to_the_top_level_clears_the_parent()
    {
        var parent = NewIssue();
        var child = NewIssue();
        child.MoveUnder(parent, [parent.Id], 0, s_now);

        Assert.True(child.MoveUnder(null, [], 0, s_now).IsSuccess);
        Assert.Null(child.ParentId);
    }

    [Fact]
    public void Links_join_two_different_issues()
    {
        var issue = NewIssue();
        Assert.Equal("issues.link_to_self", IssueLink.Create(issue, issue, IssueLinkType.Blocks, s_user, s_now).Error.Code);
    }

    [Fact]
    public void Relates_to_links_are_stored_in_one_order_so_they_cannot_be_duplicated_in_reverse()
    {
        var first = NewIssue();
        var second = NewIssue();

        var forward = IssueLink.Create(first, second, IssueLinkType.RelatesTo, s_user, s_now).Value;
        var backward = IssueLink.Create(second, first, IssueLinkType.RelatesTo, s_user, s_now).Value;

        Assert.Equal((forward.SourceIssueId, forward.TargetIssueId), (backward.SourceIssueId, backward.TargetIssueId));

        // Direction matters for "blocks".
        var blocks = IssueLink.Create(second, first, IssueLinkType.Blocks, s_user, s_now).Value;
        Assert.Equal(second.Id, blocks.SourceIssueId);
    }

    [Fact]
    public void Only_the_author_edits_a_comment_and_every_edit_keeps_the_old_text()
    {
        var comment = Comment.Create(NewIssue(), s_user, "First draft", s_now).Value;

        Assert.Equal("comments.not_author", comment.Edit(Guid.CreateVersion7(), "Hijacked", s_now).Error.Code);

        var revision = comment.Edit(s_user, "Second draft", s_now.AddMinutes(5)).Value;
        Assert.NotNull(revision);
        Assert.Equal("First draft", revision.Body);
        Assert.Equal(s_now, revision.WrittenAt);
        Assert.Equal("Second draft", comment.Body);
        Assert.Equal(s_now.AddMinutes(5), comment.EditedAt);

        // Saving the same text records nothing.
        Assert.Null(comment.Edit(s_user, " Second draft ", s_now.AddMinutes(6)).Value);
    }

    [Fact]
    public void Labels_have_a_short_name()
    {
        Assert.True(Label.Create(Guid.CreateVersion7(), "Backend", LabelColor.Teal).IsSuccess);
        Assert.Equal("labels.invalid_name", Label.Create(Guid.CreateVersion7(), " ", LabelColor.Teal).Error.Code);
        Assert.Equal("labels.invalid_name", Label.Create(Guid.CreateVersion7(), new string('x', 41), LabelColor.Teal).Error.Code);
    }
}
