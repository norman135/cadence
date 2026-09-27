using Cadence.Application.Common.ReadModels;
using Cadence.Domain.Organizations;
using Cadence.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cadence.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.Id).ValueGeneratedNever();
        builder.Property(organization => organization.Name).HasMaxLength(Organization.MaxNameLength).IsRequired();
        builder.Property(organization => organization.Slug).HasMaxLength(Organization.MaxSlugLength).IsRequired();
        builder.HasIndex(organization => organization.Slug).IsUnique();
        builder.Ignore(organization => organization.DomainEvents);
    }
}

/// <summary>
/// Composite indexes lead with <c>organization_id</c> (ADR-0007): every tenant-filtered query starts
/// by narrowing to one organization.
/// </summary>
internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).ValueGeneratedNever();
        builder.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(20);

        // One membership per user and organization; also serves "members of this organization".
        builder.HasIndex(membership => new { membership.OrganizationId, membership.UserId }).IsUnique();
        // "Organizations of this user" (the /me response and membership lookups).
        builder.HasIndex(membership => membership.UserId);

        builder.HasOne<Organization>().WithMany().HasForeignKey(membership => membership.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(membership => membership.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).ValueGeneratedNever();
        builder.Property(invitation => invitation.Email).HasMaxLength(256).IsRequired();
        builder.Property(invitation => invitation.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(32).IsRequired();
        builder.Ignore(invitation => invitation.DomainEvents);

        // Invitation links are looked up by token hash.
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        // Only pending invitations are listed or re-issued, so the index covers just those rows.
        builder.HasIndex(invitation => new { invitation.OrganizationId, invitation.Email })
            .HasDatabaseName("ix_invitations_pending")
            .HasFilter("accepted_at IS NULL AND revoked_at IS NULL");

        builder.HasOne<Organization>().WithMany().HasForeignKey(invitation => invitation.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.InvitedByUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>A read-only view over the Identity users table, for joins in application queries.</summary>
internal sealed class UserSummaryConfiguration : IEntityTypeConfiguration<UserSummary>
{
    public void Configure(EntityTypeBuilder<UserSummary> builder) =>
        builder.HasNoKey().ToSqlQuery("SELECT id, email, normalized_email, display_name FROM users");
}
