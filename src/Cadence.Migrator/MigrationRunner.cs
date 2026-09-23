using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cadence.Migrator;

internal static partial class MigrationRunner
{
    private const int MaxConnectionAttempts = 30;
    private static readonly TimeSpan s_retryDelay = TimeSpan.FromSeconds(2);

    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cadence.Migrator");

        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();

            await WaitForDatabaseAsync(db, logger, cancellationToken);

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count == 0)
            {
                LogUpToDate(logger);
                return 0;
            }

            LogApplying(logger, pending.Count, pending);
            await db.Database.MigrateAsync(cancellationToken);
            LogApplied(logger, pending.Count);

            return 0;
        }
#pragma warning disable CA1031 // A migration failure of any kind must be logged and turned into a non-zero exit code.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogFailed(logger, exception);
            return 1;
        }
    }

    /// <summary>The database container may still be starting; retry briefly before giving up.</summary>
    private static async Task WaitForDatabaseAsync(CadenceDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (await db.Database.CanConnectAsync(cancellationToken))
            {
                return;
            }

            if (attempt == MaxConnectionAttempts)
            {
                throw new InvalidOperationException($"Could not connect to the database after {attempt} attempts.");
            }

            LogWaiting(logger, attempt, MaxConnectionAttempts);
            await Task.Delay(s_retryDelay, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for the database (attempt {Attempt} of {MaxAttempts})")]
    private static partial void LogWaiting(ILogger logger, int attempt, int maxAttempts);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date")]
    private static partial void LogUpToDate(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} migration(s): {Migrations}")]
    private static partial void LogApplying(ILogger logger, int count, IEnumerable<string> migrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied {Count} migration(s)")]
    private static partial void LogApplied(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Database migration failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
