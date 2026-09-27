using Cadence.Domain.Organizations;

namespace Cadence.Application.Common.Abstractions;

/// <summary>Organization emails, queued for delivery like <see cref="IAccountEmails"/>.</summary>
public interface IOrganizationEmails
{
    ValueTask SendInvitationAsync(
        string email,
        string organizationName,
        string inviterName,
        OrganizationRole role,
        string token,
        CancellationToken cancellationToken);
}
