using Cadence.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Infrastructure.Persistence;

/// <summary>
/// Allocates issue numbers with a single atomic statement: the row lock that UPDATE takes on the
/// project serializes concurrent creates, and RETURNING hands back the new value in the same round
/// trip. It commits on its own, so a failed create leaves a gap in the numbers instead of a duplicate;
/// gaps are harmless, duplicates are not.
/// </summary>
internal sealed class IssueNumbers(CadenceDbContext db) : IIssueNumbers
{
    public async Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // ToListAsync runs the statement as written; composing operators like SingleAsync would wrap it
        // in a SELECT, and PostgreSQL doesn't allow UPDATE in a subquery.
        var numbers = await db.Database
            .SqlQuery<int>($"""
                UPDATE projects SET last_issue_number = last_issue_number + 1
                WHERE id = {projectId}
                RETURNING last_issue_number AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return numbers.Count == 1
            ? numbers[0]
            : throw new InvalidOperationException($"Project {projectId} does not exist.");
    }
}
