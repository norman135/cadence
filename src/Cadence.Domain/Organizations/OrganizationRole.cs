namespace Cadence.Domain.Organizations;

/// <summary>A member's role in an organization, from least to most privileged.</summary>
public enum OrganizationRole
{
    /// <summary>Limited access, typically an external collaborator.</summary>
    Guest = 0,

    /// <summary>A regular team member.</summary>
    Member = 1,

    /// <summary>Manages members and settings, but cannot delete the organization or manage owners.</summary>
    Admin = 2,

    /// <summary>Full control, including deleting the organization.</summary>
    Owner = 3,
}
