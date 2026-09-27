using Cadence.Application.Common.Abstractions;
using Cadence.Application.Common.Paging;
using Cadence.Domain.Common;
using Cadence.Domain.Organizations;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Members;

/// <summary>A member of the current organization.</summary>
public sealed record MemberResponse(Guid UserId, string DisplayName, string Email, OrganizationRole Role, DateTimeOffset JoinedAt);

/// <summary>Lists members in join order, one keyset page at a time.</summary>
/// <param name="After">The cursor from the previous page, if any.</param>
/// <param name="Limit">Page size, 1 to 100 (default 50).</param>
public sealed record ListMembersQuery(string? After, int? Limit) : IQuery<KeysetPage<MemberResponse>>;

public sealed class ListMembersQueryHandler(ICadenceDbContext db)
    : IQueryHandler<ListMembersQuery, KeysetPage<MemberResponse>>
{
    public async ValueTask<KeysetPage<MemberResponse>> Handle(ListMembersQuery query, CancellationToken cancellationToken)
    {
        var limit = KeysetPaging.ClampLimit(query.Limit);
        var memberships = db.Memberships.AsNoTracking();

        // Membership ids are UUIDv7, so ordering by id is ordering by join time.
        if (Guid.TryParse(query.After, out var after))
        {
            memberships = memberships.Where(membership => membership.Id > after);
        }

        var rows = await memberships
            .OrderBy(membership => membership.Id)
            .Take(limit + 1)
            .Join(
                db.Users,
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new
                {
                    membership.Id,
                    Member = new MemberResponse(user.Id, user.DisplayName, user.Email, membership.Role, membership.JoinedAt),
                })
            .ToListAsync(cancellationToken);

        var page = KeysetPaging.ToPage(rows, limit, row => row.Id.ToString());
        return new KeysetPage<MemberResponse>([.. page.Items.Select(row => row.Member)], page.NextCursor);
    }
}

/// <summary>Changes a member's role, following the ownership rules in <see cref="Membership.ChangeRole"/>.</summary>
public sealed record ChangeMemberRoleCommand(Guid UserId, OrganizationRole Role) : ICommand<Result>;

public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleCommandValidator() => RuleFor(command => command.Role).IsInEnum();
}

public sealed class ChangeMemberRoleCommandHandler(ICadenceDbContext db, ITenantContext tenant, IMembershipCache cache)
    : ICommandHandler<ChangeMemberRoleCommand, Result>
{
    public async ValueTask<Result> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
    {
        var membership = await db.Memberships.SingleOrDefaultAsync(member => member.UserId == command.UserId, cancellationToken);
        if (membership is null)
        {
            return OrganizationErrors.MemberNotFound;
        }

        var owners = await db.Memberships.CountAsync(member => member.Role == OrganizationRole.Owner, cancellationToken);
        var changed = membership.ChangeRole(tenant.Role, command.Role, owners);
        if (changed.IsFailure)
        {
            return changed;
        }

        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenant.OrganizationId, command.UserId, cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Removes a member. Members may always remove themselves (leave); removing someone else requires the
/// <see cref="Permissions.MembersManage"/> permission. The last owner can do neither.
/// </summary>
public sealed record RemoveMemberCommand(Guid UserId) : ICommand<Result>;

public sealed class RemoveMemberCommandHandler(
    ICadenceDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IMembershipCache cache)
    : ICommandHandler<RemoveMemberCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var leaving = command.UserId == currentUser.RequiredUserId;
        if (!leaving && !tenant.HasPermission(Permissions.MembersManage))
        {
            return OrganizationErrors.Forbidden;
        }

        var membership = await db.Memberships.SingleOrDefaultAsync(member => member.UserId == command.UserId, cancellationToken);
        if (membership is null)
        {
            return OrganizationErrors.MemberNotFound;
        }

        var owners = await db.Memberships.CountAsync(member => member.Role == OrganizationRole.Owner, cancellationToken);
        var removable = membership.CanBeRemoved(leaving ? membership.Role : tenant.Role, owners);
        if (removable.IsFailure)
        {
            return removable;
        }

        db.Memberships.Remove(membership);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenant.OrganizationId, command.UserId, cancellationToken);
        return Result.Success();
    }
}
