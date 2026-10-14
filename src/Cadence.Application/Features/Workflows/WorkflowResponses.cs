using System.ComponentModel;
using Cadence.Domain.Issues;
using Cadence.Domain.Workflows;

namespace Cadence.Application.Features.Workflows;

/// <summary>
/// A project's workflow as plain data: what the API returns and what the workflow cache holds.
/// Immutable, so the cache hands out the same instance instead of copies.
/// </summary>
[ImmutableObject(true)]
public sealed record WorkflowResponse(
    Guid Id,
    Guid ProjectId,
    bool RestrictTransitions,
    IReadOnlyList<WorkflowStatusResponse> Statuses,
    IReadOnlyList<WorkflowTransitionResponse> Transitions)
{
    public WorkflowStatusResponse? FindStatus(Guid statusId) => Statuses.FirstOrDefault(status => status.Id == statusId);

    public WorkflowStatusResponse DefaultStatus => Statuses.First(status => status.IsDefault);

    /// <summary>The workflow's rules, rebuilt from this data.</summary>
    public Workflow ToDomain(Guid organizationId) => Workflow.Restore(
        Id,
        organizationId,
        ProjectId,
        RestrictTransitions,
        Statuses.Select(status => (status.Id, status.Name, status.Category, status.Position, status.WipLimit, status.IsDefault)),
        Transitions.Select(transition => (transition.Id, transition.FromStatusId, transition.ToStatusId, transition.RequireAssignee, transition.AllowedBy)));

    public static WorkflowResponse From(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return new WorkflowResponse(
            workflow.Id,
            workflow.ProjectId,
            workflow.RestrictTransitions,
            [.. workflow.Statuses.Select(status => new WorkflowStatusResponse(
                status.Id, status.Name, status.Category, status.Position, status.WipLimit, status.IsDefault))],
            [.. workflow.Transitions.Select(transition => new WorkflowTransitionResponse(
                transition.Id, transition.FromStatusId, transition.ToStatusId, transition.RequireAssignee, transition.AllowedBy))]);
    }
}

/// <summary>A status of the workflow, and a column of the board.</summary>
/// <param name="Id">The status id.</param>
/// <param name="Name">The status name, unique in the workflow.</param>
/// <param name="Category">What the status counts as in filters and counters.</param>
/// <param name="Position">The board column, from 0.</param>
/// <param name="WipLimit">The most issues the column should hold, if limited.</param>
/// <param name="IsDefault">Whether new issues start here.</param>
[ImmutableObject(true)]
public sealed record WorkflowStatusResponse(Guid Id, string Name, IssueStatus Category, int Position, int? WipLimit, bool IsDefault);

/// <summary>An allowed move. Only enforced when the workflow restricts transitions.</summary>
[ImmutableObject(true)]
public sealed record WorkflowTransitionResponse(Guid Id, Guid FromStatusId, Guid ToStatusId, bool RequireAssignee, TransitionActor AllowedBy);
