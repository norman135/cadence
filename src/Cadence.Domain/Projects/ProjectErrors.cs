using Cadence.Domain.Common;

namespace Cadence.Domain.Projects;

/// <summary>Errors of the project rules. Codes are stable.</summary>
public static class ProjectErrors
{
    public static readonly Error NotFound =
        Error.NotFound("projects.not_found", "The project does not exist or you don't have access to it.");

    public static readonly Error InvalidKey =
        Error.Validation("projects.invalid_key", "Project keys are 2 to 10 uppercase letters or digits, starting with a letter.");

    public static readonly Error KeyTaken =
        Error.Conflict("projects.key_taken", "Another project in this organization already uses that key.");

    public static readonly Error InvalidName =
        Error.Validation("projects.invalid_name", "Project names must be 2 to 80 characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("projects.description_too_long", "Project descriptions must be 2,000 characters or fewer.");

    public static readonly Error Archived =
        Error.Conflict("projects.archived", "The project is archived. Restore it to make changes.");

    public static readonly Error Forbidden =
        Error.Forbidden("projects.forbidden", "You don't have permission to do that in this project.");

    public static readonly Error MemberNotFound =
        Error.NotFound("projects.member_not_found", "That person is not a member of the project.");

    public static readonly Error AlreadyMember =
        Error.Conflict("projects.already_member", "That person is already a member of the project.");

    public static readonly Error NotInOrganization =
        Error.Validation("projects.not_in_organization", "Only members of the organization can be added to its projects.");

    public static readonly Error LastAdmin =
        Error.Conflict("projects.last_admin", "A project needs at least one admin. Make someone else an admin first.");
}
