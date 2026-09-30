namespace Cadence.Application.Common.Abstractions;

/// <summary>
/// Transactional account emails. Implementations queue the message and return immediately, so HTTP
/// requests never wait on the mail server.
/// </summary>
public interface IAccountEmails
{
    ValueTask SendEmailConfirmationAsync(AuthUser user, string token, CancellationToken cancellationToken);

    ValueTask SendPasswordResetAsync(AuthUser user, string token, CancellationToken cancellationToken);
}
