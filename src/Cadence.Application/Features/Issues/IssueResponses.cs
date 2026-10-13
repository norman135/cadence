using System.Globalization;
using Cadence.Application.Features.Labels;
using Cadence.Domain.Issues;
using Cadence.Domain.Projects;

namespace Cadence.Application.Features.Issues;

/// <summary>A person shown on an issue: assignee, reporter or comment author.</summary>
public sealed record PersonSummary(Guid Id, string DisplayName);

/// <summary>One row of the issue list. Everything the list and board cards show, in one query.</summary>
public sealed record IssueListItem(
    Guid Id,
    string Key,
    Guid ProjectId,
    IssueType Type,
    string Title,
    IssueStatus Status,
    Guid StatusId,
    IssuePriority Priority,
    PersonSummary? Assignee,
    int? Estimate,
    DateOnly? DueDate,
    IReadOnlyList<LabelResponse> Labels,
    int SubIssueCount,
    int SubIssueClosedCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Another issue as referenced from this one: parent, sub-issue or link target.</summary>
public sealed record IssueReference(Guid Id, string Key, string Title, IssueStatus Status, IssueType Type);

/// <summary>
/// A link as seen from the current issue. <paramref name="Relation"/> reads from this issue's side:
/// "blocks", "blocked_by", "relates_to", "duplicates" or "duplicated_by".
/// </summary>
public sealed record IssueLinkResponse(Guid Id, string Relation, IssueReference Issue);

public sealed record IssueDetail(
    Guid Id,
    string Key,
    Guid ProjectId,
    string ProjectName,
    ProjectRole Role,
    IssueType Type,
    string Title,
    string? Description,
    IssueStatus Status,
    Guid StatusId,
    IssuePriority Priority,
    PersonSummary? Assignee,
    PersonSummary Reporter,
    int? Estimate,
    DateOnly? DueDate,
    IReadOnlyList<LabelResponse> Labels,
    IssueReference? Parent,
    IReadOnlyList<IssueReference> SubIssues,
    IReadOnlyList<IssueLinkResponse> Links,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    uint Version);

public static class IssueKeys
{
    public static string Format(string projectKey, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{projectKey}-{number}");

    /// <summary>Splits "ATL-142" into its project key and number. Keys are case-insensitive.</summary>
    public static bool TryParse(string? key, out string projectKey, out int number)
    {
        projectKey = string.Empty;
        number = 0;

        var dash = key?.LastIndexOf('-') ?? -1;
        if (dash <= 0)
        {
            return false;
        }

        var candidate = key![..dash].ToUpperInvariant();
        if (!Project.IsValidKey(candidate)
            || !int.TryParse(key.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            || number <= 0)
        {
            return false;
        }

        projectKey = candidate;
        return true;
    }
}
