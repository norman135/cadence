using Cadence.Domain.Issues;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cadence.Infrastructure.Ranking;

/// <summary>
/// Keeps rank keys short (ADR-0010). Repeatedly inserting between the same two cards lengthens
/// keys by a character every few moves; every few minutes this rewrites the ranks of any project
/// with a key longer than <see cref="Rank.RebalanceLength"/> to evenly spaced keys, in the same
/// order. A partial index makes finding those projects free when there are none.
/// </summary>
/// <remarks>
/// Rewriting changes the rows' versions, so a client holding an older version gets a 409 on its
/// next move and reloads the board. That is rare, and the alternative (unbounded keys) is not.
/// </remarks>
internal sealed partial class RankRebalancer(IServiceProvider services, ILogger<RankRebalancer> logger) : BackgroundService
{
    private static readonly TimeSpan s_interval = TimeSpan.FromMinutes(5);

    /// <summary>Rebalances every project that needs it; returns how many did.</summary>
    public async Task<int> RebalanceAsync(CancellationToken cancellationToken)
    {
        // Resolved per pass, not in the constructor: hosts without a database (build-time OpenAPI
        // generation) still start this service.
        var contexts = services.GetRequiredService<IDbContextFactory<CadenceDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var projects = await db.Issues.IgnoreQueryFilters()
            .Where(issue => issue.Rank.Length > Rank.RebalanceLength)
            .Select(issue => issue.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var projectId in projects)
        {
            // The same spacing as Rank.AtPosition: "d" and four base-62 digits, in the current order.
            var rewritten = await db.Database.ExecuteSqlAsync(
                $"""
                UPDATE issues i SET rank = 'd'
                    || substr(d.digits, (r.n / 238328) % 62 + 1, 1)
                    || substr(d.digits, (r.n / 3844) % 62 + 1, 1)
                    || substr(d.digits, (r.n / 62) % 62 + 1, 1)
                    || substr(d.digits, r.n % 62 + 1, 1)
                FROM (SELECT id, (row_number() OVER (ORDER BY rank, id) - 1)::int AS n FROM issues WHERE project_id = {projectId}) r,
                     (SELECT {Rank.Digits}::text AS digits) d
                WHERE r.id = i.id AND i.project_id = {projectId}
                """,
                cancellationToken);
            LogRebalanced(logger, projectId, rewritten);
        }

        return projects.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first pass waits a full interval, so short-lived hosts (build-time OpenAPI generation,
        // tests) never reach the database from here.
        using var timer = new PeriodicTimer(s_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RebalanceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rebalanced the ranks of project {ProjectId} ({Issues} issues)")]
    private static partial void LogRebalanced(ILogger logger, Guid projectId, int issues);

    [LoggerMessage(Level = LogLevel.Error, Message = "Rebalancing ranks failed; retrying on the next pass")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
