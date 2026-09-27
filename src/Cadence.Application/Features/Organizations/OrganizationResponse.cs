using Cadence.Domain.Organizations;
using Riok.Mapperly.Abstractions;

namespace Cadence.Application.Features.Organizations;

/// <summary>An organization, as seen by one of its members.</summary>
/// <param name="Id">The organization id, used in API routes.</param>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The URL-friendly identifier used in app routes.</param>
/// <param name="Role">The caller's role in the organization.</param>
public sealed record OrganizationResponse(Guid Id, string Name, string Slug, OrganizationRole Role);

/// <summary>Compile-time generated mappings (Mapperly, ADR-0005).</summary>
[Mapper]
internal static partial class OrganizationMappings
{
    [MapperIgnoreSource(nameof(Organization.CreatedAt))]
    [MapperIgnoreSource(nameof(Organization.DomainEvents))]
    public static partial OrganizationResponse ToResponse(this Organization organization, OrganizationRole role);
}
