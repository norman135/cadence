using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Common;
using Mediator;

namespace Cadence.Application.Features.Auth;

/// <summary>Exchanges the refresh token (from the session cookie) for new session tokens.</summary>
public sealed record RefreshSessionCommand(string? RefreshToken) : ICommand<Result<SessionTokens>>;

public sealed class RefreshSessionCommandHandler(ISessionService sessions)
    : ICommandHandler<RefreshSessionCommand, Result<SessionTokens>>
{
    public async ValueTask<Result<SessionTokens>> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        return await sessions.RefreshAsync(command.RefreshToken, cancellationToken);
    }
}

/// <summary>Signs out: ends the session the refresh token belongs to. Succeeds even without a session.</summary>
public sealed record LogoutCommand(string? RefreshToken) : ICommand<Result>;

public sealed class LogoutCommandHandler(ISessionService sessions) : ICommandHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            await sessions.EndAsync(command.RefreshToken, cancellationToken);
        }

        return Result.Success();
    }
}
