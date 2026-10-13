using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using Cadence.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cadence.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).ValueGeneratedNever();
        builder.Property(project => project.Key).HasMaxLength(Project.MaxKeyLength).IsRequired();
        builder.Property(project => project.Name).HasMaxLength(Project.MaxNameLength).IsRequired();
        builder.Property(project => project.Description).HasMaxLength(Project.MaxDescriptionLength);
        builder.Ignore(project => project.IsArchived);
        builder.Ignore(project => project.DomainEvents);

        // Keys are unique per organization and are how issue keys (ATL-142) are resolved.
        builder.HasIndex(project => new { project.OrganizationId, project.Key }).IsUnique();

        builder.HasOne<Organization>().WithMany().HasForeignKey(project => project.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(project => project.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).ValueGeneratedNever();
        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(20);

        // One membership per user and project; also serves "members of this project".
        builder.HasIndex(member => new { member.ProjectId, member.UserId }).IsUnique();
        // "Projects this user can see" in an organization.
        builder.HasIndex(member => new { member.OrganizationId, member.UserId });

        builder.HasOne<Project>().WithMany().HasForeignKey(member => member.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
