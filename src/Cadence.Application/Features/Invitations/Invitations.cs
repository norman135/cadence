using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Cadence.Domain.Common;
using Cadence.Domain.Organizations;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Invitations;

/// <summary>A pending invitation, as listed for the organization's administrators.</summary>
public sealed record InvitationResponse(
    Guid Id,
    string Email,
    OrganizationRole Role,
    string InvitedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Invites an email address to the current organization. A pending invitation for the same address
/// is revoked, so only the newest link works.
/// </summary>
public sealed record CreateInvitationCommand(string Email, OrganizationRole Role) : ICommand<Result<InvitationResponse>>;

public sealed class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Role).IsInEnum();
    }
}

public sealed class CreateInvitationCommandHandler(
    ICadenceDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    IOrganizationEmails emails,
    TimeProvider timeProvider)
    : ICommandHandler<CreateInvitationCommand, Result<InvitationResponse>>
{
    public async ValueTask<Result<InvitationResponse>> Handle(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var email = Invitation.NormalizeEmail(command.Email);
        var normalizedForIdentity = email.ToUpperInvariant();

        var alreadyMember = await db.Memberships.AnyAsync(
            membership => db.Users.Any(user => user.Id == membership.UserId && user.NormalizedEmail == normalizedForIdentity),
            cancellationToken);
        if (alreadyMember)
        {
            return OrganizationErrors.AlreadyMember;
        }

        var created = Invitation.Create(tenant.OrganizationId, email, command.Role, tenant.Role, currentUser.RequiredUserId, now);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var (invitation, token) = created.Value;

        await db.Invitations
            .Where(pending => pending.Email == email && pending.AcceptedAt == null && pending.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(pending => pending.RevokedAt, now), cancellationToken);

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        var organizationName = await db.Organizations
            .Where(organization => organization.Id == tenant.OrganizationId)
            .Select(organization => organization.Name)
            .SingleAsync(cancellationToken);
        var inviterName = currentUser.DisplayName ?? "A teammate";

        await emails.SendInvitationAsync(email, organizationName, inviterName, invitation.Role, token, cancellationToken);

        return new InvitationResponse(invitation.Id, invitation.Email, invitation.Role, inviterName, invitation.CreatedAt, invitation.ExpiresAt);
    }
}

/// <summary>Lists the current organization's pending invitations, newest first.</summary>
public sealed record ListInvitationsQuery : IQuery<IReadOnlyList<InvitationResponse>>;

public sealed class ListInvitationsQueryHandler(ICadenceDbContext db, TimeProvider timeProvider)
    : IQueryHandler<ListInvitationsQuery, IReadOnlyList<InvitationResponse>>
{
    public async ValueTask<IReadOnlyList<InvitationResponse>> Handle(ListInvitationsQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await db.Invitations.AsNoTracking()
            .Where(invitation => invitation.AcceptedAt == null && invitation.RevokedAt == null && invitation.ExpiresAt > now)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Join(
                db.Users,
                invitation => invitation.InvitedByUserId,
                user => user.Id,
                (invitation, user) => new InvitationResponse(
                    invitation.Id, invitation.Email, invitation.Role, user.DisplayName, invitation.CreatedAt, invitation.ExpiresAt))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Revokes a pending invitation; its link stops working immediately.</summary>
public sealed record RevokeInvitationCommand(Guid InvitationId) : ICommand<Result>;

public sealed class RevokeInvitationCommandHandler(ICadenceDbContext db, TimeProvider timeProvider)
    : ICommandHandler<RevokeInvitationCommand, Result>
{
    public async ValueTask<Result> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations.SingleOrDefaultAsync(invitation => invitation.Id == command.InvitationId, cancellationToken);
        if (invitation is null)
        {
            return OrganizationErrors.InvitationNotFound;
        }

        var revoked = invitation.Revoke(timeProvider.GetUtcNow());
        if (revoked.IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return revoked;
    }
}

/// <summary>What an invitation link shows before it is accepted. Available without signing in.</summary>
public sealed record InvitationPreviewResponse(
    string OrganizationName,
    string InvitedByName,
    string Email,
    OrganizationRole Role,
    InvitationStatus Status,
    DateTimeOffset ExpiresAt);

public sealed record GetInvitationQuery(string Token) : IQuery<Result<InvitationPreviewResponse>>;

public sealed class GetInvitationQueryHandler(ICadenceDbContext db, TimeProvider timeProvider)
    : IQueryHandler<GetInvitationQuery, Result<InvitationPreviewResponse>>
{
    public async ValueTask<Result<InvitationPreviewResponse>> Handle(GetInvitationQuery query, CancellationToken cancellationToken)
    {
        var hash = Invitation.HashToken(query.Token);

        // The token itself grants access to the preview, so the tenant filter does not apply here.
        var found = await db.Invitations.IgnoreQueryFilters([QueryFilters.Tenant]).AsNoTracking()
            .Where(invitation => invitation.TokenHash == hash)
            .Select(invitation => new
            {
                Invitation = invitation,
                OrganizationName = db.Organizations
                    .Where(organization => organization.Id == invitation.OrganizationId)
                    .Select(organization => organization.Name)
                    .Single(),
                InvitedByName = db.Users
                    .Where(user => user.Id == invitation.InvitedByUserId)
                    .Select(user => user.DisplayName)
                    .Single(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return OrganizationErrors.InvitationNotFound;
        }

        var invitation = found.Invitation;
        return new InvitationPreviewResponse(
            found.OrganizationName,
            found.InvitedByName,
            invitation.Email,
            invitation.Role,
            invitation.StatusAt(timeProvider.GetUtcNow()),
            invitation.ExpiresAt);
    }
}

/// <summary>Where to go after accepting an invitation.</summary>
public sealed record AcceptInvitationResponse(Guid OrganizationId, string Name, string Slug, OrganizationRole Role);

/// <summary>Accepts an invitation addressed to the signed-in user's email and joins the organization.</summary>
public sealed record AcceptInvitationCommand(string Token) : ICommand<Result<AcceptInvitationResponse>>;

public sealed class AcceptInvitationCommandHandler(
    ICadenceDbContext db,
    ICurrentUser currentUser,
    IMembershipCache cache,
    TimeProvider timeProvider)
    : ICommandHandler<AcceptInvitationCommand, Result<AcceptInvitationResponse>>
{
    public async ValueTask<Result<AcceptInvitationResponse>> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequiredUserId;
        var now = timeProvider.GetUtcNow();
        var hash = Invitation.HashToken(command.Token);

        var invitation = await db.Invitations.IgnoreQueryFilters([QueryFilters.Tenant])
            .SingleOrDefaultAsync(invitation => invitation.TokenHash == hash, cancellationToken);
        if (invitation is null)
        {
            return OrganizationErrors.InvitationNotFound;
        }

        var email = await db.Users.Where(user => user.Id == userId).Select(user => user.Email).SingleAsync(cancellationToken);
        var accepted = invitation.Accept(userId, email, now);
        if (accepted.IsFailure)
        {
            return accepted.Error;
        }

        var existing = await db.Memberships.IgnoreQueryFilters([QueryFilters.Tenant])
            .Where(membership => membership.OrganizationId == invitation.OrganizationId && membership.UserId == userId)
            .Select(membership => (OrganizationRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            db.Memberships.Add(Membership.Create(invitation.OrganizationId, userId, invitation.Role, now));
        }

        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(invitation.OrganizationId, userId, cancellationToken);

        var organization = await db.Organizations.AsNoTracking()
            .Where(organization => organization.Id == invitation.OrganizationId)
            .Select(organization => new { organization.Name, organization.Slug })
            .SingleAsync(cancellationToken);

        return new AcceptInvitationResponse(invitation.OrganizationId, organization.Name, organization.Slug, existing ?? invitation.Role);
    }
}
