using Cadence.Domain.Common;

namespace Cadence.Domain.Workflows;

/// <summary>Errors of the workflow rules. Codes are stable.</summary>
public static class WorkflowErrors
{
    public static readonly Error StatusCount =
        Error.Validation("workflows.status_count", $"A workflow has 1 to {Workflow.MaxStatuses} statuses.");

    public static readonly Error InvalidStatusName =
        Error.Validation("workflows.invalid_status_name", $"Status names must be 1 to {WorkflowStatus.MaxNameLength} characters.");

    public static readonly Error DuplicateStatusName =
        Error.Validation("workflows.duplicate_status_name", "Each status in a workflow needs its own name.");

    public static readonly Error InvalidWipLimit =
        Error.Validation("workflows.invalid_wip_limit", $"WIP limits must be between 1 and {WorkflowStatus.MaxWipLimit}.");

    public static readonly Error DefaultStatus =
        Error.Validation("workflows.default_status", "Exactly one status must be the default, and it can't be a done or canceled status.");

    public static readonly Error UnknownStatus =
        Error.Validation("workflows.unknown_status", "That status isn't part of this project's workflow.");

    public static readonly Error InvalidTransition =
        Error.Validation("workflows.invalid_transition", "Transitions must connect two different statuses of the workflow.");

    public static readonly Error ReplacementRequired =
        Error.Validation("workflows.replacement_required", "Choose where the issues of each removed status should go.");

    public static Error TransitionNotAllowed(string from, string to) =>
        Error.Validation("workflows.transition_not_allowed", $"This workflow doesn't allow moving issues from {from} to {to}.");

    public static Error TransitionRestricted(string to, TransitionActor allowedBy) =>
        Error.Forbidden(
            "workflows.transition_restricted",
            allowedBy == TransitionActor.Admins
                ? $"Only project admins can move issues to {to}."
                : $"Only the assignee or a project admin can move this issue to {to}.");

    public static Error AssigneeRequired(string to) =>
        Error.Validation("workflows.assignee_required", $"Assign the issue before moving it to {to}.");
}
