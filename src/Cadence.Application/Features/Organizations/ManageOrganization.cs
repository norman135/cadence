using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Common;
using Cadence.Domain.Organizations;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Organizations;

/// <summary>Returns the current organization with the caller's role.</summary>
public sealed record GetOrganizationQuery : IQuery<Result<OrganizationResponse>>;

public sealed class GetOrganizationQueryHandler(ICadenceDbContext db, ITenantContext tenant)
    : IQueryHandler<GetOrganizationQuery, Result<OrganizationResponse>>
{
    public async ValueTask<Result<OrganizationResponse>> Handle(GetOrganizationQuery query, CancellationToken cancellationToken)
    {
        var role = tenant.Role;
        var organization = await db.Organizations.AsNoTracking()
            .Where(organization => organization.Id == tenant.OrganizationId)
            .Select(organization => new OrganizationResponse(organization.Id, organization.Name, organization.Slug, role))
            .SingleOrDefaultAsync(cancellationToken);

        return organization is null ? OrganizationErrors.NotFound : organization;
    }
}

/// <summary>Renames the current organization. The slug stays the same, so links keep working.</summary>
public sealed record RenameOrganizationCommand(string Name) : ICommand<Result>;

public sealed class RenameOrganizationCommandValidator : AbstractValidator<RenameOrganizationCommand>
{
    public RenameOrganizationCommandValidator() =>
        RuleFor(command => command.Name)
            .Must(name => name.Trim().Length is >= Organization.MinNameLength and <= Organization.MaxNameLength)
            .WithMessage($"Name must be {Organization.MinNameLength} to {Organization.MaxNameLength} characters.");
}

public sealed class RenameOrganizationCommandHandler(ICadenceDbContext db, ITenantContext tenant)
    : ICommandHandler<RenameOrganizationCommand, Result>
{
    public async ValueTask<Result> Handle(RenameOrganizationCommand command, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.SingleOrDefaultAsync(
            organization => organization.Id == tenant.OrganizationId, cancellationToken);

        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        var renamed = organization.Rename(command.Name);
        if (renamed.IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return renamed;
    }
}

/// <summary>
/// Deletes the current organization. Memberships and invitations are removed by the database's
/// cascading foreign keys, in one statement.
/// </summary>
public sealed record DeleteOrganizationCommand : ICommand<Result>;

public sealed class DeleteOrganizationCommandHandler(ICadenceDbContext db, ITenantContext tenant, IMembershipCache memberships)
    : ICommandHandler<DeleteOrganizationCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteOrganizationCommand command, CancellationToken cancellationToken)
    {
        var organizationId = tenant.OrganizationId;
        await db.Organizations
            .Where(organization => organization.Id == organizationId)
            .ExecuteDeleteAsync(cancellationToken);

        await memberships.InvalidateOrganizationAsync(organizationId, cancellationToken);
        return Result.Success();
    }
}
