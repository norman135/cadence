namespace Cadence.Api.Hosting;

internal static partial class ApiLog
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "{ApplicationName} {Version} started ({Environment})")]
    public static partial void ApplicationStarted(this ILogger logger, string applicationName, string version, string environment);
}
