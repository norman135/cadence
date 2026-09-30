using System.Security.Cryptography;
using Cadence.Application.Common.Abstractions;
using Cadence.Domain.Common;
using Cadence.Domain.Organizations;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Application.Features.Organizations;

/// <summary>Creates an organization with the caller as its first owner.</summary>
public sealed record CreateOrganizationCommand(string Name) : ICommand<Result<OrganizationResponse>>;

public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator() =>
        RuleFor(command => command.Name)
            .Must(name => name.Trim().Length is >= Organization.MinNameLength and <= Organization.MaxNameLength)
            .WithMessage($"Name must be {Organization.MinNameLength} to {Organization.MaxNameLength} characters.");
}

public sealed class CreateOrganizationCommandHandler(
    ICadenceDbContext db,
    ICurrentUser currentUser,
    IMembershipCache memberships,
    TimeProvider timeProvider)
    : ICommandHandler<CreateOrganizationCommand, Result<OrganizationResponse>>
{
    private const string SuffixAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public async ValueTask<Result<OrganizationResponse>> Handle(CreateOrganizationCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequiredUserId;
        var now = timeProvider.GetUtcNow();

        var created = Organization.Create(command.Name, await UniqueSlugAsync(command.Name, cancellationToken), now);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var organization = created.Value;
        db.Organizations.Add(organization);
        db.Memberships.Add(Membership.Create(organization.Id, userId, OrganizationRole.Owner, now));
        await db.SaveChangesAsync(cancellationToken);

        await memberships.InvalidateAsync(organization.Id, userId, cancellationToken);
        return organization.ToResponse(OrganizationRole.Owner);
    }

    /// <summary>"Acme" becomes "acme"; if that is taken, "acme-x7k2", and so on.</summary>
    private async Task<string> UniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = Organization.SlugFrom(name);
        var candidate = baseSlug;

        while (await db.Organizations.AnyAsync(organization => organization.Slug == candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{RandomNumberGenerator.GetString(SuffixAlphabet, 4)}";
        }

        return candidate;
    }
}
