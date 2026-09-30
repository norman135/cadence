using Cadence.Domain.Common;

namespace Cadence.Domain.Organizations;

/// <summary>Errors of the organization, membership and invitation rules. Codes are stable.</summary>
public static class OrganizationErrors
{
    public static readonly Error NotFound =
        Error.NotFound("organizations.not_found", "The organization does not exist or you are not a member.");

    public static readonly Error InvalidName =
        Error.Validation("organizations.invalid_name", "Organization names must be 2 to 80 characters.");

    public static readonly Error Forbidden =
        Error.Forbidden("members.forbidden", "You don't have permission to do that.");

    public static readonly Error MemberNotFound =
        Error.NotFound("members.not_found", "The member does not exist.");

    public static readonly Error OwnerRequired =
        Error.Forbidden("members.owner_required", "Only owners can grant or change the owner role.");

    public static readonly Error LastOwner =
        Error.Conflict("members.last_owner", "An organization needs at least one owner. Make someone else an owner first.");

    public static readonly Error AlreadyMember =
        Error.Conflict("invitations.already_member", "This person is already a member of the organization.");

    public static readonly Error InvitationNotFound =
        Error.NotFound("invitations.not_found", "The invitation does not exist.");

    public static readonly Error InvitationNotPending =
        Error.Conflict("invitations.not_pending", "The invitation has already been used, revoked or has expired.");

    public static readonly Error InvitationEmailMismatch =
        Error.Forbidden("invitations.email_mismatch", "This invitation was sent to a different email address.");
}
