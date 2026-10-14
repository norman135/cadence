namespace Cadence.Domain.Issues;

public enum IssueType
{
    Task,
    Bug,
    Story,

    /// <summary>A large body of work that groups other issues. Epics are always top-level.</summary>
    Epic,
}

/// <summary>
/// The category of an issue's status: where it is in its life. The categories are fixed; each
/// project's workflow defines named statuses, each mapped to one of them.
/// </summary>
public enum IssueStatus
{
    Backlog,
    Todo,
    InProgress,
    InReview,
    Done,
    Canceled,
}

/// <summary>Stored as these numbers, so sorting by priority is sorting by value.</summary>
public enum IssuePriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4,
}

public static class IssueStatusExtensions
{
    /// <summary>Done or canceled: no longer counted as open work.</summary>
    public static bool IsClosed(this IssueStatus status) => status is IssueStatus.Done or IssueStatus.Canceled;
}
