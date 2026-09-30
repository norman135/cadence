using Cadence.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Cadence.Api.Hosting;

/// <summary>Requires a permission in the request's organization (see <c>Permissions</c>).</summary>
internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Creates a policy on demand for every <c>permission:&lt;name&gt;</c> policy name, so endpoints can
/// declare <c>.RequirePermission(Permissions.MembersInvite)</c> without registering policies up front.
/// </summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "permission:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
            .Build();
    }
}

/// <summary>
/// Grants a permission requirement from the membership that tenant resolution already loaded, so
/// the check itself makes no database query.
/// </summary>
internal sealed class PermissionAuthorizationHandler(ITenantContext tenant) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (tenant.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal static class PermissionEndpointExtensions
{
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionPolicyProvider.Prefix + permission);
}
