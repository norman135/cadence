using Cadence.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace Cadence.Infrastructure.Email;

/// <summary>Composes the account emails and queues them for delivery.</summary>
internal sealed class AccountEmails(EmailQueue queue, IOptions<PublicUrlOptions> urls) : IAccountEmails
{
    public ValueTask SendEmailConfirmationAsync(AuthUser user, string token, CancellationToken cancellationToken)
    {
        var link = urls.Value.Link("/confirm-email", ("userId", user.Id.ToString()), ("token", token));

        var (html, text) = EmailTemplate.ActionEmail(
            heading: "Confirm your email address",
            greeting: $"Hi {user.DisplayName},",
            body: "Welcome to Cadence! Confirm your email address to finish creating your account.",
            actionLabel: "Confirm email",
            actionUrl: link,
            footnote: "The link expires in 24 hours. If you didn't create a Cadence account, you can ignore this email.");

        return queue.EnqueueAsync(
            new EmailMessage(user.Email, user.DisplayName, "Confirm your email address", html, text),
            cancellationToken);
    }

    public ValueTask SendPasswordResetAsync(AuthUser user, string token, CancellationToken cancellationToken)
    {
        var link = urls.Value.Link("/reset-password", ("email", user.Email), ("token", token));

        var (html, text) = EmailTemplate.ActionEmail(
            heading: "Reset your password",
            greeting: $"Hi {user.DisplayName},",
            body: "We received a request to reset your Cadence password. Choose a new one with the button below.",
            actionLabel: "Reset password",
            actionUrl: link,
            footnote: "The link expires in 2 hours. If you didn't ask for a reset, you can ignore this email; your password stays the same.");

        return queue.EnqueueAsync(
            new EmailMessage(user.Email, user.DisplayName, "Reset your Cadence password", html, text),
            cancellationToken);
    }
}
