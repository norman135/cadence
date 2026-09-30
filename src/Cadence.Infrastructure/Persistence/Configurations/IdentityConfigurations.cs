using Cadence.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cadence.Infrastructure.Persistence.Configurations;

/// <summary>Maps the Identity store to plain table names (users, user_claims, …) instead of AspNet*.</summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");
        builder.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.CreatedAt).IsRequired();

        // Identity checks email uniqueness in code; the unique index also closes the race between two
        // concurrent registrations with the same address.
        builder.HasIndex(user => user.NormalizedEmail).IsUnique().HasDatabaseName("ix_users_normalized_email");
        builder.HasIndex(user => user.NormalizedUserName).IsUnique().HasDatabaseName("ix_users_normalized_user_name");
    }
}

internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("user_claims");
}

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("user_logins");
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("user_tokens");
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        // Every refresh looks tokens up by hash.
        builder.Property(token => token.TokenHash).HasMaxLength(32).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Ending sessions updates tokens by family (sign-out, reuse) or by user (password change).
        builder.HasIndex(token => token.FamilyId);
        builder.HasIndex(token => token.UserId);

        builder.Property(token => token.RevocationReason).HasConversion<string>().HasMaxLength(20);
        builder.Property(token => token.Version).IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
