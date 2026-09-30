namespace Cadence.Domain.Common;

/// <summary>
/// Data that belongs to exactly one organization (tenant). The persistence layer filters every query
/// on these types to the current organization and refuses to save them into another one (ADR-0007).
/// </summary>
public interface ITenantScoped
{
    Guid OrganizationId { get; }
}
