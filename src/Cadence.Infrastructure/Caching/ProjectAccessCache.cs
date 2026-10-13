using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Projects;
using Cadence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Cadence.Infrastructure.Caching;

/// <summary>
/// Project memberships cached in-process with HybridCache, like <see cref="MembershipCache"/>. Entries
/// share the organization's tag, so deleting an organization (or removing a member from it) clears
/// them along with the organization memberships.
/// </summary>
internal sealed class ProjectAccessCache(HybridCache cache, IDbContextFactory<CadenceDbContext> contexts) : IProjectAccessCache
{
    private static readonly HybridCacheEntryOptions s_entryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10),
    };

    public async ValueTask<ProjectRole?> GetRoleAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        // HybridCache can't store a null value type distinctly, so the role is cached as a nullable box.
        var role = await cache.GetOrCreateAsync(
            RoleKey(projectId, userId),
            (organizationId, projectId, userId, contexts),
            static async (state, token) =>
            {
                await using var db = await state.contexts.CreateDbContextAsync(token);
                var roles = await db.ProjectMembers.IgnoreQueryFilters([QueryFilters.Tenant]).AsNoTracking()
                    .Where(member => member.OrganizationId == state.organizationId
                        && member.ProjectId == state.projectId
                        && member.UserId == state.userId)
                    .Select(member => member.Role)
                    .ToListAsync(token);
                return new CachedRole(roles.Count == 0 ? null : roles[0]);
            },
            s_entryOptions,
            [OrganizationTag(organizationId), UserTag(organizationId, userId)],
            cancellationToken);

        return role.Value;
    }

    public async ValueTask<IReadOnlyList<Guid>> GetProjectIdsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            ProjectsKey(organizationId, userId),
            (organizationId, userId, contexts),
            static async (state, token) =>
            {
                await using var db = await state.contexts.CreateDbContextAsync(token);
                return await db.ProjectMembers.IgnoreQueryFilters([QueryFilters.Tenant]).AsNoTracking()
                    .Where(member => member.OrganizationId == state.organizationId && member.UserId == state.userId)
                    .Select(member => member.ProjectId)
                    .ToArrayAsync(token);
            },
            s_entryOptions,
            [OrganizationTag(organizationId), UserTag(organizationId, userId)],
            cancellationToken);

    public async ValueTask InvalidateAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(RoleKey(projectId, userId), cancellationToken);
        await cache.RemoveAsync(ProjectsKey(organizationId, userId), cancellationToken);
    }

    public ValueTask InvalidateUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(UserTag(organizationId, userId), cancellationToken);

    private static string RoleKey(Guid projectId, Guid userId) => $"project-role:{projectId:N}:{userId:N}";

    private static string ProjectsKey(Guid organizationId, Guid userId) => $"projects-of:{organizationId:N}:{userId:N}";

    // Must match MembershipCache, so InvalidateOrganizationAsync clears project entries too.
    private static string OrganizationTag(Guid organizationId) => $"org:{organizationId:N}";

    private static string UserTag(Guid organizationId, Guid userId) => $"org-user:{organizationId:N}:{userId:N}";

    /// <summary>A role, or none, as a cacheable value.</summary>
    private sealed record CachedRole(ProjectRole? Value);
}
