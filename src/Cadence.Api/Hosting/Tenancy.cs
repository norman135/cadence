using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Organizations;

namespace Cadence.Api.Hosting;

/// <summary>The request's organization, set by <see cref="TenantResolutionMiddleware"/>.</summary>
internal sealed class HttpTenantContext : ITenantContext
{
    private MembershipSnapshot? _membership;

    public bool IsResolved => _membership is not null;

    public Guid OrganizationId => Membership.OrganizationId;

    public OrganizationRole Role => Membership.Role;

    private MembershipSnapshot Membership =>
        _membership ?? throw new InvalidOperationException("No organization was resolved for this request.");

    public void Resolve(MembershipSnapshot membership) => _membership = membership;
}

/// <summary>
/// For routes under <c>/organizations/{organizationId}</c>, looks up the caller's membership (cached)
/// and binds the request to that organization. Non-members get the same 404 as a missing
/// organization, so an organization's existence is never revealed to outsiders.
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string RouteParameter = "organizationId";

    public async Task InvokeAsync(
        HttpContext context,
        HttpTenantContext tenant,
        ICurrentUser currentUser,
        IMembershipCache memberships)
    {
        // Anonymous requests fall through; authorization answers them with 401.
        if (context.GetRouteValue(RouteParameter) is string value
            && Guid.TryParse(value, out var organizationId)
            && currentUser.UserId is { } userId)
        {
            var membership = await memberships.GetAsync(organizationId, userId, context.RequestAborted);
            if (membership is null)
            {
                await OrganizationErrors.NotFound.ToProblem().ExecuteAsync(context);
                return;
            }

            tenant.Resolve(membership);
        }

        await next(context);
    }
}
