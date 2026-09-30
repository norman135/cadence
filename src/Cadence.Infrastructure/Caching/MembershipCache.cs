using Cadence.Application.Common.Abstractions;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Cadence.Infrastructure.Caching;

/// <summary>
/// Memberships cached in-process with HybridCache (no Redis, ADR-0008). Every tenant-scoped request
/// needs one, so a hit keeps the permission check free of database queries. Concurrent misses for
/// the same key share one query (stampede protection). Entries expire after 10 minutes and are
/// removed explicitly whenever a membership changes.
/// </summary>
internal sealed class MembershipCache(HybridCache cache, IDbContextFactory<CadenceDbContext> contexts) : IMembershipCache
{
    private static readonly HybridCacheEntryOptions s_entryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10),
    };

    public ValueTask<MembershipSnapshot?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            Key(organizationId, userId),
            (organizationId, userId, contexts),
            static async (state, token) =>
            {
                await using var db = await state.contexts.CreateDbContextAsync(token);
                return await db.Memberships.IgnoreQueryFilters([QueryFilters.Tenant]).AsNoTracking()
                    .Where(membership => membership.OrganizationId == state.organizationId && membership.UserId == state.userId)
                    .Select(membership => new MembershipSnapshot(membership.OrganizationId, membership.UserId, membership.Role))
                    .SingleOrDefaultAsync(token);
            },
            s_entryOptions,
            [OrganizationTag(organizationId)],
            cancellationToken);

    public ValueTask InvalidateAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(Key(organizationId, userId), cancellationToken);

    public ValueTask InvalidateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(OrganizationTag(organizationId), cancellationToken);

    private static string Key(Guid organizationId, Guid userId) => $"membership:{organizationId:N}:{userId:N}";

    private static string OrganizationTag(Guid organizationId) => $"org:{organizationId:N}";
}
