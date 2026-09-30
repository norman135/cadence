using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Organizations;
using Microsoft.Extensions.Options;

namespace Cadence.Infrastructure.Email;

/// <summary>Composes organization emails and queues them for delivery.</summary>
internal sealed class OrganizationEmails(EmailQueue queue, IOptions<PublicUrlOptions> urls) : IOrganizationEmails
{
    public ValueTask SendInvitationAsync(
        string email,
        string organizationName,
        string inviterName,
        OrganizationRole role,
        string token,
        CancellationToken cancellationToken)
    {
        var link = urls.Value.Link($"/invitations/{Uri.EscapeDataString(token)}");
        var article = role is OrganizationRole.Admin or OrganizationRole.Owner ? "an" : "a";

        var (html, text) = EmailTemplate.ActionEmail(
            heading: $"Join {organizationName} on Cadence",
            greeting: "Hi,",
            body: $"{inviterName} invited you to join {organizationName} as {article} {role.ToString().ToLowerInvariant()}.",
            actionLabel: "Accept invitation",
            actionUrl: link,
            footnote: "The invitation expires in 7 days. If you weren't expecting it, you can ignore this email.");

        return queue.EnqueueAsync(
            new EmailMessage(email, email, $"{inviterName} invited you to {organizationName}", html, text),
            cancellationToken);
    }
}
