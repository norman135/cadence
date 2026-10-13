using Cadence.Domain.Common;
using Cadence.Domain.Projects;

namespace Cadence.Domain.Issues;

/// <summary>
/// A unit of work in a project, known to people by its key (<c>ATL-142</c>): the project key and a
/// number that is unique in the project and never reused.
/// </summary>
public sealed class Issue : AggregateRoot<Guid>, ITenantScoped
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 50_000;
    public const int MaxEstimate = 100;
    public const int MaxLabels = 10;

    /// <summary>Levels below the top: an epic, its issues, and their sub-issues.</summary>
    public const int MaxDepth = 2;

    private Issue(
        Guid id,
        Guid organizationId,
        Guid projectId,
        int number,
        IssueType type,
        string title,
        string? description,
        IssueStatus status,
        IssuePriority priority,
        Guid reporterId,
        DateTimeOffset now)
        : base(id)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
        Number = number;
        Type = type;
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        ReporterId = reporterId;
        CreatedAt = now;
        UpdatedAt = now;
        CompletedAt = status.IsClosed() ? now : null;
    }

    /// <summary>Used by EF Core.</summary>
    private Issue()
    {
        Title = string.Empty;
    }

    public Guid OrganizationId { get; private init; }

    public Guid ProjectId { get; private init; }

    /// <summary>The issue's number in its project, the "142" in <c>ATL-142</c>.</summary>
    public int Number { get; private init; }

    public IssueType Type { get; private set; }

    public string Title { get; private set; }

    /// <summary>Markdown.</summary>
    public string? Description { get; private set; }

    public IssueStatus Status { get; private set; }

    public IssuePriority Priority { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public Guid ReporterId { get; private init; }

    /// <summary>Story points, 0 to 100.</summary>
    public int? Estimate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Guid? ParentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>When the issue was last closed (done or canceled), for cycle-time reporting.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Denormalized: direct sub-issues, and how many of them are closed.</summary>
    public int SubIssueCount { get; private init; }

    public int SubIssueClosedCount { get; private init; }

    /// <summary>Denormalized: comments on the issue.</summary>
    public int CommentCount { get; private init; }

    public static Result<Issue> Create(
        Project project,
        int number,
        IssueType type,
        string title,
        string? description,
        IssueStatus status,
        IssuePriority priority,
        Guid reporterId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.IsArchived)
        {
            return ProjectErrors.Archived;
        }

        var trimmedTitle = title.Trim();
        if (!IsValidTitle(trimmedTitle))
        {
            return IssueErrors.InvalidTitle;
        }

        var normalizedDescription = NormalizeDescription(description);
        if (normalizedDescription?.Length > MaxDescriptionLength)
        {
            return IssueErrors.DescriptionTooLong;
        }

        return new Issue(
            Guid.CreateVersion7(),
            project.OrganizationId,
            project.Id,
            number,
            type,
            trimmedTitle,
            normalizedDescription,
            status,
            priority,
            reporterId,
            now);
    }

    public Result Retitle(string title, DateTimeOffset now)
    {
        var trimmed = title.Trim();
        if (!IsValidTitle(trimmed))
        {
            return IssueErrors.InvalidTitle;
        }

        Title = trimmed;
        return Touch(now);
    }

    public Result Describe(string? description, DateTimeOffset now)
    {
        var normalized = NormalizeDescription(description);
        if (normalized?.Length > MaxDescriptionLength)
        {
            return IssueErrors.DescriptionTooLong;
        }

        Description = normalized;
        return Touch(now);
    }

    /// <summary>Moves the issue to a status; closing it records when, reopening clears that.</summary>
    public Result MoveTo(IssueStatus status, DateTimeOffset now)
    {
        if (status.IsClosed() && !Status.IsClosed())
        {
            CompletedAt = now;
        }
        else if (!status.IsClosed())
        {
            CompletedAt = null;
        }

        Status = status;
        return Touch(now);
    }

    public Result Prioritize(IssuePriority priority, DateTimeOffset now)
    {
        Priority = priority;
        return Touch(now);
    }

    /// <summary>Assigns the issue; callers check that the assignee has access to the project.</summary>
    public Result AssignTo(Guid? assigneeId, DateTimeOffset now)
    {
        AssigneeId = assigneeId;
        return Touch(now);
    }

    public Result EstimateAt(int? points, DateTimeOffset now)
    {
        if (points is < 0 or > MaxEstimate)
        {
            return IssueErrors.InvalidEstimate;
        }

        Estimate = points;
        return Touch(now);
    }

    public Result DueOn(DateOnly? dueDate, DateTimeOffset now)
    {
        DueDate = dueDate;
        return Touch(now);
    }

    public Result ChangeType(IssueType type, DateTimeOffset now)
    {
        if (type == IssueType.Epic && ParentId is not null)
        {
            return IssueErrors.EpicCannotHaveParent;
        }

        Type = type;
        return Touch(now);
    }

    /// <summary>
    /// Places the issue under <paramref name="parent"/>, or at the top level when it is
    /// <see langword="null"/>. Nesting is limited to three levels including the moved issue's own
    /// sub-issues, epics stay top-level, and an issue can't end up under itself.
    /// </summary>
    /// <param name="parent">The new parent, or <see langword="null"/>.</param>
    /// <param name="parentAncestry">The parent and all of its ancestors (closest first).</param>
    /// <param name="subtreeHeight">Levels of sub-issues below this issue: 0 without sub-issues.</param>
    /// <param name="now">The current time.</param>
    public Result MoveUnder(Issue? parent, IReadOnlyList<Guid> parentAncestry, int subtreeHeight, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(parentAncestry);

        if (parent is null)
        {
            ParentId = null;
            return Touch(now);
        }

        if (Type == IssueType.Epic)
        {
            return IssueErrors.EpicCannotHaveParent;
        }

        if (parent.ProjectId != ProjectId)
        {
            return IssueErrors.ParentInOtherProject;
        }

        if (parent.Id == Id || parentAncestry.Contains(Id))
        {
            return IssueErrors.ParentCycle;
        }

        // The parent sits at depth (ancestry - 1); this issue goes one below it.
        if (parentAncestry.Count + subtreeHeight > MaxDepth)
        {
            return IssueErrors.TooDeep;
        }

        ParentId = parent.Id;
        return Touch(now);
    }

    /// <summary>Records that the labels changed; they are stored outside the aggregate.</summary>
    public void LabelsChanged(DateTimeOffset now) => UpdatedAt = now;

    private Result Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        return Result.Success();
    }

    private static bool IsValidTitle(string title) => title.Length is >= 1 and <= MaxTitleLength;

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.TrimEnd();
}
