using Microsoft.Extensions.Logging;

namespace Cadence.Application.Common.Behaviors;

/// <summary>Source-generated, allocation-free log messages for the request pipeline.</summary>
internal static partial class BehaviorLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "Handled {RequestName} in {ElapsedMilliseconds:0.0} ms")]
    public static partial void RequestHandled(this ILogger logger, string requestName, double elapsedMilliseconds);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Slow request: {RequestName} took {ElapsedMilliseconds:0.0} ms")]
    public static partial void SlowRequest(this ILogger logger, string requestName, double elapsedMilliseconds);
}
