using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Cadence.Domain.Common;
using Cadence.Domain.Organizations;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Me;

/// <summary>Returns the signed-in user's profile and the organizations they belong to.</summary>
public sealed record GetCurrentUserQuery : IQuery<Result<CurrentUserResponse>>;

/// <summary>The signed-in user.</summary>
public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName, IReadOnlyList<MyOrganizationResponse> Organizations);

/// <summary>An organization the signed-in user belongs to, with their role in it.</summary>
public sealed record MyOrganizationResponse(Guid Id, string Name, string Slug, OrganizationRole Role);

public sealed class GetCurrentUserQueryHandler(ICadenceDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    public async ValueTask<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequiredUserId;

        var user = await db.Users
            .Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return AuthErrors.UserNotFound;
        }

        // Spans every organization of the user, so the tenant filter does not apply.
        var organizations = await (
                from membership in db.Memberships.IgnoreQueryFilters([QueryFilters.Tenant]).AsNoTracking()
                join organization in db.Organizations on membership.OrganizationId equals organization.Id
                where membership.UserId == userId
                orderby organization.Name
                select new MyOrganizationResponse(organization.Id, organization.Name, organization.Slug, membership.Role))
            .ToListAsync(cancellationToken);

        return new CurrentUserResponse(userId, user.Email, user.DisplayName, organizations);
    }
}
