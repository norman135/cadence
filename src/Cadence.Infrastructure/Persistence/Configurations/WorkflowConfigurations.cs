using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cadence.Infrastructure.Persistence.Configurations;

/// <summary>One workflow per project. Statuses and transitions are owned by it and saved with it.</summary>
internal sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflows");
        builder.HasKey(workflow => workflow.Id);
        builder.Property(workflow => workflow.Id).ValueGeneratedNever();
        builder.Ignore(workflow => workflow.DomainEvents);
        builder.Ignore(workflow => workflow.Statuses);
        builder.Ignore(workflow => workflow.Transitions);
        builder.Ignore(workflow => workflow.DefaultStatus);

        builder.HasIndex(workflow => workflow.ProjectId).IsUnique();

        // The aggregate exposes ordered, read-only views; EF works with the backing lists.
        builder.HasMany<WorkflowStatus>("_statuses").WithOne().HasForeignKey(status => status.WorkflowId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<WorkflowTransition>("_transitions").WithOne().HasForeignKey(transition => transition.WorkflowId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Organization>().WithMany().HasForeignKey(workflow => workflow.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Project>().WithMany().HasForeignKey(workflow => workflow.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.ToTable("workflow_statuses");
        builder.HasKey(status => status.Id);
        builder.Property(status => status.Id).ValueGeneratedNever();
        builder.Property(status => status.Name).HasMaxLength(WorkflowStatus.MaxNameLength).IsRequired();

        // Stored as the same integers as issues.status, so moving issues between statuses can copy it.
        builder.Property(status => status.Category);

        builder.HasIndex(status => status.WorkflowId);
        builder.HasIndex(status => status.ProjectId);
    }
}

internal sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("workflow_transitions");
        builder.HasKey(transition => transition.Id);
        builder.Property(transition => transition.Id).ValueGeneratedNever();
        builder.Property(transition => transition.AllowedBy).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(transition => new { transition.WorkflowId, transition.FromStatusId, transition.ToStatusId }).IsUnique();

        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(transition => transition.FromStatusId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(transition => transition.ToStatusId).OnDelete(DeleteBehavior.Cascade);
    }
}
