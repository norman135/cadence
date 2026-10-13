using Cadence.Domain.Common;

namespace Cadence.Domain.Issues;

/// <summary>Errors of the issue, label, link and comment rules. Codes are stable.</summary>
public static class IssueErrors
{
    public static readonly Error NotFound =
        Error.NotFound("issues.not_found", "The issue does not exist or you don't have access to it.");

    public static readonly Error InvalidTitle =
        Error.Validation("issues.invalid_title", "Issue titles must be 1 to 200 characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("issues.description_too_long", "Issue descriptions must be 50,000 characters or fewer.");

    public static readonly Error InvalidEstimate =
        Error.Validation("issues.invalid_estimate", "Estimates must be between 0 and 100 points.");

    public static readonly Error ParentInOtherProject =
        Error.Validation("issues.parent_in_other_project", "A parent issue must be in the same project.");

    public static readonly Error EpicCannotHaveParent =
        Error.Validation("issues.epic_cannot_have_parent", "Epics are top-level and can't have a parent issue.");

    public static readonly Error ParentCycle =
        Error.Validation("issues.parent_cycle", "An issue can't be placed under itself or one of its own sub-issues.");

    public static readonly Error TooDeep =
        Error.Validation("issues.too_deep", "Issues can be nested at most three levels deep.");

    public static readonly Error ParentNotFound =
        Error.Validation("issues.parent_not_found", "The parent issue does not exist.");

    public static readonly Error AssigneeNotInProject =
        Error.Validation("issues.assignee_not_in_project", "Issues can only be assigned to people with access to the project.");

    public static readonly Error LinkToSelf =
        Error.Validation("issues.link_to_self", "An issue can't be linked to itself.");

    public static readonly Error LinkExists =
        Error.Conflict("issues.link_exists", "These issues are already linked that way.");

    public static readonly Error LinkNotFound =
        Error.NotFound("issues.link_not_found", "The link does not exist.");

    public static readonly Error LabelNotFound =
        Error.Validation("labels.not_found", "One or more labels do not exist.");

    public static readonly Error InvalidLabelName =
        Error.Validation("labels.invalid_name", "Label names must be 1 to 40 characters.");

    public static readonly Error LabelNameTaken =
        Error.Conflict("labels.name_taken", "A label with that name already exists.");

    public static readonly Error TooManyLabels =
        Error.Validation("issues.too_many_labels", "An issue can have at most 10 labels.");

    public static readonly Error CommentNotFound =
        Error.NotFound("comments.not_found", "The comment does not exist.");

    public static readonly Error InvalidCommentBody =
        Error.Validation("comments.invalid_body", "Comments must be 1 to 20,000 characters.");

    public static readonly Error NotCommentAuthor =
        Error.Forbidden("comments.not_author", "Only the author can edit a comment.");
}
