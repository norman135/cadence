using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cadence.Infrastructure.Persistence.Interceptors;

/// <summary>Settings for database diagnostics (<c>Cadence:Database</c>).</summary>
public sealed class DatabaseDiagnosticsOptions
{
    public const string SectionName = "Cadence:Database";

    /// <summary>Commands slower than this are logged as warnings. Defaults to 50 ms.</summary>
    public int SlowQueryThresholdMilliseconds { get; set; } = 50;
}

/// <summary>
/// Logs every database command that exceeds the slow-query threshold, with its SQL text and duration.
/// Parameter values are never logged. Stateless and safe to share across pooled DbContexts.
/// </summary>
internal sealed class SlowQueryInterceptor(
    ILogger<SlowQueryInterceptor> logger,
    IOptions<DatabaseDiagnosticsOptions> options) : DbCommandInterceptor
{
    private const int MaxLoggedCommandLength = 2_000;

    private readonly TimeSpan _threshold = TimeSpan.FromMilliseconds(options.Value.SlowQueryThresholdMilliseconds);

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Check(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Check(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Check(command, eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return ValueTask.FromResult(result);
    }

    private void Check(DbCommand command, CommandExecutedEventData eventData)
    {
        if (eventData.Duration < _threshold)
        {
            return;
        }

        var sql = command.CommandText.Length > MaxLoggedCommandLength
            ? string.Concat(command.CommandText.AsSpan(0, MaxLoggedCommandLength), "…")
            : command.CommandText;

        logger.SlowQuery(eventData.Duration.TotalMilliseconds, sql);
    }
}

internal static partial class SlowQueryLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "Slow database command ({ElapsedMilliseconds:0.0} ms): {CommandText}")]
    public static partial void SlowQuery(this ILogger logger, double elapsedMilliseconds, string commandText);
}
