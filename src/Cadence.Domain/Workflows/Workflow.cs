using Cadence.Domain.Common;
using Cadence.Domain.Issues;
using Cadence.Domain.Projects;

namespace Cadence.Domain.Workflows;

/// <summary>
/// A project's workflow: the statuses its issues move through (the board's columns, in order),
/// each mapped to one of the fixed <see cref="IssueStatus"/> categories, and optionally which moves
/// between them are allowed and by whom.
/// </summary>
/// <remarks>
/// Categories keep filters, counters and reports working across projects with different
/// workflows: an issue stores both its status and that status's category.
/// </remarks>
public sealed class Workflow : AggregateRoot<Guid>, ITenantScoped
{
    public const int MaxStatuses = 20;

    private readonly List<WorkflowStatus> _statuses = [];
    private readonly List<WorkflowTransition> _transitions = [];

    private Workflow(Guid id, Guid organizationId, Guid projectId)
        : base(id)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
    }

    /// <summary>Used by EF Core.</summary>
    private Workflow()
    {
    }

    public Guid OrganizationId { get; private init; }

    public Guid ProjectId { get; private init; }

    /// <summary>
    /// When <see langword="false"/>, an issue may move between any two statuses. When
    /// <see langword="true"/>, only the listed <see cref="Transitions"/> are allowed.
    /// </summary>
    public bool RestrictTransitions { get; private set; }

    /// <summary>In board order.</summary>
    public IReadOnlyList<WorkflowStatus> Statuses => _statuses.OrderBy(status => status.Position).ToList();

    public IReadOnlyList<WorkflowTransition> Transitions => _transitions;

    /// <summary>Where new issues start.</summary>
    public WorkflowStatus DefaultStatus => _statuses.Single(status => status.IsDefault);

    /// <summary>
    /// The template every new project starts with: one status per category, any move allowed.
    /// </summary>
    public static Workflow CreateDefault(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var workflow = new Workflow(Guid.CreateVersion7(), project.OrganizationId, project.Id);
        (string Name, IssueStatus Category)[] template =
        [
            ("Backlog", IssueStatus.Backlog),
            ("Todo", IssueStatus.Todo),
            ("In progress", IssueStatus.InProgress),
            ("In review", IssueStatus.InReview),
            ("Done", IssueStatus.Done),
            ("Canceled", IssueStatus.Canceled),
        ];

        for (var position = 0; position < template.Length; position++)
        {
            var (name, category) = template[position];
            workflow._statuses.Add(new WorkflowStatus(
                Guid.CreateVersion7(), workflow, name, category, position, wipLimit: null, isDefault: category == IssueStatus.Todo));
        }

        return workflow;
    }

    /// <summary>
    /// Rebuilds a stored workflow from its parts, for callers that cache workflows as plain data and
    /// need the rules (<see cref="CanMove"/>) without loading the aggregate.
    /// </summary>
    public static Workflow Restore(
        Guid id,
        Guid organizationId,
        Guid projectId,
        bool restrictTransitions,
        IEnumerable<(Guid Id, string Name, IssueStatus Category, int Position, int? WipLimit, bool IsDefault)> statuses,
        IEnumerable<(Guid Id, Guid FromStatusId, Guid ToStatusId, bool RequireAssignee, TransitionActor AllowedBy)> transitions)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(transitions);

        var workflow = new Workflow(id, organizationId, projectId) { RestrictTransitions = restrictTransitions };
        foreach (var status in statuses)
        {
            workflow._statuses.Add(new WorkflowStatus(status.Id, workflow, status.Name, status.Category, status.Position, status.WipLimit, status.IsDefault));
        }

        foreach (var transition in transitions)
        {
            workflow._transitions.Add(new WorkflowTransition(
                transition.Id, workflow, transition.FromStatusId, transition.ToStatusId, transition.RequireAssignee, transition.AllowedBy));
        }

        return workflow;
    }

    public WorkflowStatus? FindStatus(Guid statusId) => _statuses.Find(status => status.Id == statusId);

    /// <summary>The default status's counterpart for a category: the first status in it, if any.</summary>
    public WorkflowStatus? FirstIn(IssueStatus category) => Statuses.FirstOrDefault(status => status.Category == category);

    /// <summary>
    /// Replaces the statuses and transitions. Statuses with an id are kept and updated; others are
    /// added; statuses left out are removed. The caller moves the removed statuses' issues and
    /// recategorizes issues whose status changed category.
    /// </summary>
    /// <param name="statuses">Every status, in board order.</param>
    /// <param name="restrictTransitions">Whether only the listed transitions are allowed.</param>
    /// <param name="transitions">Allowed moves, referring to <paramref name="statuses"/> by index.</param>
    /// <returns>What changed, so the caller can move and recount issues.</returns>
    public Result<WorkflowChanges> Redefine(
        IReadOnlyList<StatusDraft> statuses,
        bool restrictTransitions,
        IReadOnlyList<TransitionDraft> transitions)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(transitions);

        var error = Validate(statuses, transitions);
        if (error is not null)
        {
            return error;
        }

        var kept = statuses.Where(draft => draft.Id is not null).Select(draft => draft.Id!.Value).ToHashSet();
        var removed = _statuses.Where(status => !kept.Contains(status.Id)).ToList();
        var recategorized = new List<Guid>();

        var resolved = new List<WorkflowStatus>(statuses.Count);
        for (var position = 0; position < statuses.Count; position++)
        {
            var draft = statuses[position];
            var name = draft.Name.Trim();
            if (draft.Id is { } id)
            {
                var existing = FindStatus(id);
                if (existing is null)
                {
                    return WorkflowErrors.UnknownStatus;
                }

                if (existing.Category != draft.Category)
                {
                    recategorized.Add(existing.Id);
                }

                existing.Update(name, draft.Category, position, draft.WipLimit, draft.IsDefault);
                resolved.Add(existing);
            }
            else
            {
                var added = new WorkflowStatus(Guid.CreateVersion7(), this, name, draft.Category, position, draft.WipLimit, draft.IsDefault);
                _statuses.Add(added);
                resolved.Add(added);
            }
        }

        foreach (var status in removed)
        {
            _statuses.Remove(status);
        }

        RestrictTransitions = restrictTransitions;
        _transitions.Clear();
        foreach (var draft in transitions.DistinctBy(draft => (draft.From, draft.To)))
        {
            _transitions.Add(new WorkflowTransition(
                Guid.CreateVersion7(), this, resolved[draft.From].Id, resolved[draft.To].Id, draft.RequireAssignee, draft.AllowedBy));
        }

        return new WorkflowChanges([.. removed.Select(status => status.Id)], recategorized);
    }

    /// <summary>
    /// Whether someone may move an issue from one status to another. Reordering within a status is
    /// always allowed.
    /// </summary>
    /// <param name="fromStatusId">The issue's current status.</param>
    /// <param name="toStatusId">The status it moves to.</param>
    /// <param name="assigneeId">The issue's assignee.</param>
    /// <param name="actorId">Who is moving it.</param>
    /// <param name="actorRole">Their effective role in the project.</param>
    public Result CanMove(Guid fromStatusId, Guid toStatusId, Guid? assigneeId, Guid actorId, ProjectRole actorRole)
    {
        var to = FindStatus(toStatusId);
        if (to is null || FindStatus(fromStatusId) is null)
        {
            return WorkflowErrors.UnknownStatus;
        }

        if (fromStatusId == toStatusId || !RestrictTransitions)
        {
            return Result.Success();
        }

        var transition = _transitions.Find(candidate => candidate.FromStatusId == fromStatusId && candidate.ToStatusId == toStatusId);
        if (transition is null)
        {
            return WorkflowErrors.TransitionNotAllowed(FindStatus(fromStatusId)!.Name, to.Name);
        }

        var allowed = transition.AllowedBy switch
        {
            TransitionActor.Admins => actorRole == ProjectRole.Admin,
            TransitionActor.AssigneeOrAdmins => actorRole == ProjectRole.Admin || (assigneeId is not null && assigneeId == actorId),
            _ => true,
        };
        if (!allowed)
        {
            return WorkflowErrors.TransitionRestricted(to.Name, transition.AllowedBy);
        }

        if (transition.RequireAssignee && assigneeId is null)
        {
            return WorkflowErrors.AssigneeRequired(to.Name);
        }

        return Result.Success();
    }

    /// <summary>Whether a move between these statuses depends on who the assignee is.</summary>
    public bool MoveDependsOnAssignee(Guid fromStatusId, Guid toStatusId) =>
        RestrictTransitions
        && fromStatusId != toStatusId
        && _transitions.Exists(transition =>
            transition.FromStatusId == fromStatusId
            && transition.ToStatusId == toStatusId
            && (transition.RequireAssignee || transition.AllowedBy == TransitionActor.AssigneeOrAdmins));

    private static Error? Validate(IReadOnlyList<StatusDraft> statuses, IReadOnlyList<TransitionDraft> transitions)
    {
        if (statuses.Count is 0 or > MaxStatuses)
        {
            return WorkflowErrors.StatusCount;
        }

        if (statuses.Any(draft => draft.Name.Trim().Length is 0 or > WorkflowStatus.MaxNameLength))
        {
            return WorkflowErrors.InvalidStatusName;
        }

        if (statuses.Select(draft => draft.Name.Trim().ToUpperInvariant()).Distinct().Count() != statuses.Count)
        {
            return WorkflowErrors.DuplicateStatusName;
        }

        if (statuses.Any(draft => draft.WipLimit is < 1 or > WorkflowStatus.MaxWipLimit))
        {
            return WorkflowErrors.InvalidWipLimit;
        }

        var defaults = statuses.Where(draft => draft.IsDefault).ToList();
        if (defaults.Count != 1 || defaults[0].Category.IsClosed())
        {
            return WorkflowErrors.DefaultStatus;
        }

        if (statuses.Where(draft => draft.Id is not null).GroupBy(draft => draft.Id).Any(group => group.Count() > 1))
        {
            return WorkflowErrors.UnknownStatus;
        }

        if (transitions.Any(draft => draft.From < 0 || draft.From >= statuses.Count || draft.To < 0 || draft.To >= statuses.Count || draft.From == draft.To))
        {
            return WorkflowErrors.InvalidTransition;
        }

        return null;
    }
}

/// <summary>A status as submitted from the workflow editor: an existing one (with its id) or a new one.</summary>
public sealed record StatusDraft(Guid? Id, string Name, IssueStatus Category, int? WipLimit, bool IsDefault);

/// <summary>An allowed move between two statuses, by their positions in the submitted list.</summary>
public sealed record TransitionDraft(int From, int To, bool RequireAssignee, TransitionActor AllowedBy);

/// <summary>Statuses that were removed, and kept statuses whose category changed.</summary>
public sealed record WorkflowChanges(IReadOnlyList<Guid> RemovedStatusIds, IReadOnlyList<Guid> RecategorizedStatusIds);
