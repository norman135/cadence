using Cadence.Domain.Issues;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using Cadence.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cadence.Infrastructure.Persistence.Configurations;

/// <summary>
/// Issues are the hottest table: the list view filters and sorts them, so each common access path
/// has an index that leads with the tenant or project and ends with the keyset sort columns.
/// Enums are stored as integers, so priority sorts by value and status filters are cheap.
/// </summary>
internal sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    /// <summary>Open issues: every status before Done (4) and Canceled (5).</summary>
    public const string OpenFilter = "status < 4";

    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("issues");
        builder.HasKey(issue => issue.Id);
        builder.Property(issue => issue.Id).ValueGeneratedNever();
        builder.Property(issue => issue.Title).HasMaxLength(Issue.MaxTitleLength).IsRequired();
        builder.Property(issue => issue.Description).HasMaxLength(Issue.MaxDescriptionLength);
        builder.Ignore(issue => issue.DomainEvents);

        // Rank keys compare byte by byte (ADR-0010), so the column uses the "C" collation.
        builder.Property(issue => issue.Rank).HasMaxLength(Rank.MaxLength).UseCollation("C").IsRequired();

        // PostgreSQL's xmin system column: a free row version for optimistic concurrency.
        builder.Property(issue => issue.Version).IsRowVersion();

        // ATL-142 resolves to (project, number), and numbers never repeat in a project.
        builder.HasIndex(issue => new { issue.ProjectId, issue.Number }).IsUnique();

        // The default list sort (recently updated first), across the organization and per project.
        builder.HasIndex(issue => new { issue.OrganizationId, issue.UpdatedAt, issue.Id })
            .IsDescending(false, true, true);
        builder.HasIndex(issue => new { issue.ProjectId, issue.UpdatedAt, issue.Id })
            .IsDescending(false, true, true);

        // "Assigned to me and still open": My work and the assignee filter. Closed issues, the bulk
        // of an old project, stay out of the index.
        builder.HasIndex(issue => new { issue.AssigneeId, issue.UpdatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_issues_assignee_open")
            .HasFilter(OpenFilter);

        // A project's order: board columns read it by rank, filtered to the column's statuses.
        builder.HasIndex(issue => new { issue.ProjectId, issue.Rank });

        // Projects whose keys have grown long, for the rebalancing job. Almost always empty.
        builder.HasIndex(issue => issue.ProjectId)
            .HasDatabaseName("ix_issues_long_ranks")
            .HasFilter($"length(rank) > {Rank.RebalanceLength}");

        // Sub-issues of a parent. Most issues have no parent, so only those that do are indexed.
        builder.HasIndex(issue => issue.ParentId)
            .HasDatabaseName("ix_issues_parent")
            .HasFilter("parent_id IS NOT NULL");

        builder.HasOne<Organization>().WithMany().HasForeignKey(issue => issue.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Project>().WithMany().HasForeignKey(issue => issue.ProjectId).OnDelete(DeleteBehavior.Cascade);
        // Deleting a parent promotes its sub-issues to the top level instead of deleting them.
        builder.HasOne<Issue>().WithMany().HasForeignKey(issue => issue.ParentId).OnDelete(DeleteBehavior.SetNull);
        // A status with issues can't be deleted. NO ACTION (not RESTRICT) lets the migration make the
        // check deferred, so the workflow editor can delete statuses and move their issues in one
        // transaction.
        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(issue => issue.StatusId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(issue => issue.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(issue => issue.ReporterId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");
        builder.HasKey(label => label.Id);
        builder.Property(label => label.Id).ValueGeneratedNever();
        builder.Property(label => label.Name).HasMaxLength(Label.MaxNameLength).IsRequired();
        builder.Property(label => label.Color).HasConversion<string>().HasMaxLength(10);

        // Names are unique per organization ignoring case: a unique index on lower(name) is created
        // in the migration (expression indexes aren't modelled). This one serves listing.
        builder.HasIndex(label => label.OrganizationId);

        builder.HasOne<Organization>().WithMany().HasForeignKey(label => label.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IssueLabelConfiguration : IEntityTypeConfiguration<IssueLabel>
{
    public void Configure(EntityTypeBuilder<IssueLabel> builder)
    {
        builder.ToTable("issue_labels");
        builder.HasKey(issueLabel => new { issueLabel.IssueId, issueLabel.LabelId });

        // The label filter: issues carrying a label.
        builder.HasIndex(issueLabel => new { issueLabel.LabelId, issueLabel.IssueId });

        builder.HasOne<Issue>().WithMany().HasForeignKey(issueLabel => issueLabel.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Label>().WithMany().HasForeignKey(issueLabel => issueLabel.LabelId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class IssueLinkConfiguration : IEntityTypeConfiguration<IssueLink>
{
    public void Configure(EntityTypeBuilder<IssueLink> builder)
    {
        builder.ToTable("issue_links");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).ValueGeneratedNever();

        builder.HasIndex(link => new { link.SourceIssueId, link.TargetIssueId, link.Type }).IsUnique();
        // Links are shown on both issues, so they are looked up from either end.
        builder.HasIndex(link => link.TargetIssueId);

        builder.HasOne<Issue>().WithMany().HasForeignKey(link => link.SourceIssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Issue>().WithMany().HasForeignKey(link => link.TargetIssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(link => link.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).ValueGeneratedNever();
        builder.Property(comment => comment.Body).HasMaxLength(Comment.MaxBodyLength).IsRequired();

        // An issue's comments in order (UUIDv7 ids sort by creation time).
        builder.HasIndex(comment => new { comment.IssueId, comment.Id });

        builder.HasOne<Issue>().WithMany().HasForeignKey(comment => comment.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(comment => comment.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CommentRevisionConfiguration : IEntityTypeConfiguration<CommentRevision>
{
    public void Configure(EntityTypeBuilder<CommentRevision> builder)
    {
        builder.ToTable("comment_revisions");
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.Id).ValueGeneratedNever();
        builder.Property(revision => revision.Body).HasMaxLength(Comment.MaxBodyLength).IsRequired();

        builder.HasIndex(revision => new { revision.CommentId, revision.Id });

        builder.HasOne<Comment>().WithMany().HasForeignKey(revision => revision.CommentId).OnDelete(DeleteBehavior.Cascade);
    }
}
