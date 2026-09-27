using Mediator;
using Microsoft.Extensions.Logging;

namespace Cadence.Application.Common.Behaviors;

/// <summary>
/// Measures every request and logs slow ones as warnings. Fast requests are logged at debug level
/// only, so production logs stay small.
/// </summary>
public sealed class LoggingBehavior<TMessage, TResponse>(
    ILogger<LoggingBehavior<TMessage, TResponse>> logger,
    TimeProvider timeProvider)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    internal static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromMilliseconds(500);

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var startedAt = timeProvider.GetTimestamp();
        var response = await next(message, cancellationToken);
        var elapsed = timeProvider.GetElapsedTime(startedAt);

        if (elapsed >= SlowRequestThreshold)
        {
            logger.SlowRequest(typeof(TMessage).Name, elapsed.TotalMilliseconds);
        }
        else
        {
            logger.RequestHandled(typeof(TMessage).Name, elapsed.TotalMilliseconds);
        }

        return response;
    }
}
