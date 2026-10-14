using Cadence.Domain.Common;

namespace Cadence.Domain.Workflows;

/// <summary>Who may make a transition.</summary>
public enum TransitionActor
{
    /// <summary>Anyone who can edit issues in the project.</summary>
    Anyone,

    /// <summary>The issue's assignee, or a project admin.</summary>
    AssigneeOrAdmins,

    /// <summary>Project admins only.</summary>
    Admins,
}

/// <summary>
/// An allowed move between two statuses of a workflow that restricts transitions, with the rules
/// that apply to it.
/// </summary>
public sealed class WorkflowTransition : Entity<Guid>, ITenantScoped
{
    internal WorkflowTransition(Guid id, Workflow workflow, Guid fromStatusId, Guid toStatusId, bool requireAssignee, TransitionActor allowedBy)
        : base(id)
    {
        OrganizationId = workflow.OrganizationId;
        WorkflowId = workflow.Id;
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
        RequireAssignee = requireAssignee;
        AllowedBy = allowedBy;
    }

    /// <summary>Used by EF Core.</summary>
    private WorkflowTransition()
    {
    }

    public Guid OrganizationId { get; private init; }

    public Guid WorkflowId { get; private init; }

    public Guid FromStatusId { get; private init; }

    public Guid ToStatusId { get; private init; }

    /// <summary>The issue must have an assignee to make this move.</summary>
    public bool RequireAssignee { get; private init; }

    public TransitionActor AllowedBy { get; private init; }
}
