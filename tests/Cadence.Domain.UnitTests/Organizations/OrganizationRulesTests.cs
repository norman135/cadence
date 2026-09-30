using Cadence.Domain.Organizations;

namespace Cadence.Domain.UnitTests.Organizations;

public sealed class OrganizationRulesTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Acme Corp!", "acme-corp")]
    [InlineData("  Café Société ", "cafe-societe")]
    [InlineData("R&D -- Team 42", "r-d-team-42")]
    [InlineData("!!!", "org")]
    public void Slugs_are_lowercase_ascii_words_joined_by_hyphens(string name, string slug) =>
        Assert.Equal(slug, Organization.SlugFrom(name));

    [Fact]
    public void Slugs_leave_room_for_a_uniqueness_suffix() =>
        Assert.True(Organization.SlugFrom(new string('a', 200)).Length <= Organization.MaxSlugLength - 5);

    [Theory]
    [InlineData("A", false)]
    [InlineData("Ab", true)]
    [InlineData(" Ab ", true)]
    public void Names_are_two_to_eighty_characters_after_trimming(string name, bool valid) =>
        Assert.Equal(valid, Organization.Create(name, "slug", s_now).IsSuccess);

    [Fact]
    public void Each_role_includes_the_permissions_of_the_roles_below_it()
    {
        Assert.Empty(Permissions.For(OrganizationRole.Guest));
        Assert.Subset(Permissions.For(OrganizationRole.Admin), Permissions.For(OrganizationRole.Member));
        Assert.Subset(Permissions.For(OrganizationRole.Owner), Permissions.For(OrganizationRole.Admin));
        Assert.True(OrganizationRole.Owner.Grants(Permissions.OrganizationDelete));
        Assert.False(OrganizationRole.Admin.Grants(Permissions.OrganizationDelete));
    }

    [Theory]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Member, OrganizationRole.Owner, 1, "members.owner_required")]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Owner, OrganizationRole.Member, 2, "members.owner_required")]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Owner, OrganizationRole.Admin, 1, "members.last_owner")]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Owner, OrganizationRole.Admin, 2, null)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Member, OrganizationRole.Admin, 1, null)]
    public void Role_changes_follow_the_ownership_rules(
        OrganizationRole actor, OrganizationRole current, OrganizationRole next, int owners, string? error)
    {
        var membership = Membership.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), current, s_now);

        var result = membership.ChangeRole(actor, next, owners);

        Assert.Equal(error, result.IsFailure ? result.Error.Code : null);
        Assert.Equal(error is null ? next : current, membership.Role);
    }

    [Fact]
    public void Invitations_are_single_use_and_bound_to_their_email()
    {
        var (invitation, token) = Invitation.Create(Guid.CreateVersion7(), " Ada@Example.com ", OrganizationRole.Member, OrganizationRole.Admin, Guid.CreateVersion7(), s_now).Value;

        Assert.Equal("ada@example.com", invitation.Email);
        Assert.Equal(Invitation.HashToken(token), invitation.TokenHash);
        Assert.Equal("invitations.email_mismatch", invitation.Accept(Guid.CreateVersion7(), "eve@example.com", s_now).Error.Code);
        Assert.True(invitation.Accept(Guid.CreateVersion7(), "ADA@example.com", s_now).IsSuccess);
        Assert.Equal("invitations.not_pending", invitation.Accept(Guid.CreateVersion7(), "ada@example.com", s_now).Error.Code);
    }

    [Fact]
    public void Invitations_expire_after_seven_days()
    {
        var (invitation, _) = Invitation.Create(Guid.CreateVersion7(), "ada@example.com", OrganizationRole.Member, OrganizationRole.Owner, Guid.CreateVersion7(), s_now).Value;

        Assert.Equal(InvitationStatus.Pending, invitation.StatusAt(s_now + TimeSpan.FromDays(6)));
        Assert.Equal(InvitationStatus.Expired, invitation.StatusAt(s_now + Invitation.Lifetime));
    }

    [Fact]
    public void Only_owners_may_invite_owners() =>
        Assert.Equal(
            "members.owner_required",
            Invitation.Create(Guid.CreateVersion7(), "ada@example.com", OrganizationRole.Owner, OrganizationRole.Admin, Guid.CreateVersion7(), s_now).Error.Code);
}
