using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Workflows;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Cadence.Infrastructure.Caching;

/// <summary>
/// Workflows cached in-process with HybridCache. The cached value is immutable, so hits return the
/// stored instance without copying. Entries carry the organization tag, so deleting the
/// organization clears them too.
/// </summary>
internal sealed class WorkflowCache(HybridCache cache, IDbContextFactory<CadenceDbContext> contexts) : IWorkflowCache
{
    private static readonly HybridCacheEntryOptions s_entryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(30),
    };

    public async ValueTask<WorkflowResponse?> GetAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var cached = await cache.GetOrCreateAsync(
            Key(projectId),
            (organizationId, projectId, contexts),
            static async (state, token) =>
            {
                await using var db = await state.contexts.CreateDbContextAsync(token);
                var workflow = await db.Workflows.IgnoreQueryFilters([QueryFilters.Tenant]).AsNoTracking()
                    .Include("_statuses")
                    .Include("_transitions")
                    .AsSplitQuery()
                    .SingleOrDefaultAsync(candidate => candidate.ProjectId == state.projectId, token);
                return new CachedWorkflow(workflow is null ? null : (workflow.OrganizationId, WorkflowResponse.From(workflow)));
            },
            s_entryOptions,
            [$"org:{organizationId:N}"],
            cancellationToken);

        // Another organization's project id resolves to nothing, exactly like a missing project.
        return cached.Value is { } value && value.OrganizationId == organizationId ? value.Workflow : null;
    }

    public ValueTask InvalidateAsync(Guid projectId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(Key(projectId), cancellationToken);

    private static string Key(Guid projectId) => $"workflow:{projectId:N}";

    /// <summary>A workflow, or none, with its organization, as a cacheable value.</summary>
    [System.ComponentModel.ImmutableObject(true)]
    private sealed record CachedWorkflow((Guid OrganizationId, WorkflowResponse Workflow)? Value);
}
