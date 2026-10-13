using Cadence.Domain.Common;
using Cadence.Domain.Issues;

namespace Cadence.Domain.Workflows;

/// <summary>A named step in a workflow, and a column on the project's board.</summary>
public sealed class WorkflowStatus : Entity<Guid>, ITenantScoped
{
    public const int MaxNameLength = 40;
    public const int MaxWipLimit = 999;

    internal WorkflowStatus(Guid id, Workflow workflow, string name, IssueStatus category, int position, int? wipLimit, bool isDefault)
        : base(id)
    {
        OrganizationId = workflow.OrganizationId;
        WorkflowId = workflow.Id;
        ProjectId = workflow.ProjectId;
        Name = name;
        Category = category;
        Position = position;
        WipLimit = wipLimit;
        IsDefault = isDefault;
    }

    /// <summary>Used by EF Core.</summary>
    private WorkflowStatus()
    {
        Name = string.Empty;
    }

    public Guid OrganizationId { get; private init; }

    public Guid WorkflowId { get; private init; }

    /// <summary>Denormalized from the workflow, so issues' statuses can be checked against their project.</summary>
    public Guid ProjectId { get; private init; }

    public string Name { get; private set; }

    /// <summary>The fixed category this status counts as in filters, counters and reports.</summary>
    public IssueStatus Category { get; private set; }

    /// <summary>The column's place on the board, from 0.</summary>
    public int Position { get; private set; }

    /// <summary>The most issues the column should hold; the board warns above it. None when null.</summary>
    public int? WipLimit { get; private set; }

    /// <summary>Whether new issues start here. Exactly one status per workflow is the default.</summary>
    public bool IsDefault { get; private set; }

    internal void Update(string name, IssueStatus category, int position, int? wipLimit, bool isDefault)
    {
        Name = name;
        Category = category;
        Position = position;
        WipLimit = wipLimit;
        IsDefault = isDefault;
    }
}
