using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;

namespace Cadence.Domain.UnitTests.Projects;

public sealed class ProjectRulesTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_organization = Guid.CreateVersion7();
    private static readonly Guid s_user = Guid.CreateVersion7();

    [Theory]
    [InlineData("ATL", true)]
    [InlineData(" atl ", true)]
    [InlineData("A1", true)]
    [InlineData("A", false)]
    [InlineData("1AB", false)]
    [InlineData("AT-L", false)]
    [InlineData("ABCDEFGHIJK", false)]
    public void Keys_are_two_to_ten_letters_or_digits_starting_with_a_letter(string key, bool valid) =>
        Assert.Equal(valid, Project.Create(s_organization, key, "Atlas", null, s_user, s_now).IsSuccess);

    [Fact]
    public void Keys_are_stored_in_uppercase() =>
        Assert.Equal("ATL", Project.Create(s_organization, "atl", "Atlas", null, s_user, s_now).Value.Key);

    [Theory]
    [InlineData("Atlas Mobile", "AM")]
    [InlineData("Atlas", "ATL")]
    [InlineData("Go", "GO")]
    [InlineData("X", "XX")]
    [InlineData("123 !!", "PRJ")]
    [InlineData("Nimbus API v2", "NAV")]
    public void Suggested_keys_use_initials_or_the_first_letters(string name, string key)
    {
        Assert.Equal(key, Project.SuggestKey(name));
        Assert.True(Project.IsValidKey(Project.SuggestKey(name)));
    }

    [Fact]
    public void Descriptions_are_optional_and_trimmed()
    {
        var project = Project.Create(s_organization, "ATL", "Atlas", "  ", s_user, s_now).Value;
        Assert.Null(project.Description);

        project.Describe("  Mobile app  ");
        Assert.Equal("Mobile app", project.Description);
        Assert.False(project.Describe(new string('x', Project.MaxDescriptionLength + 1)).IsSuccess);
    }

    [Fact]
    public void Organization_owners_and_admins_manage_every_project()
    {
        Assert.Equal(ProjectRole.Admin, ProjectAccess.EffectiveRole(OrganizationRole.Owner, projectRole: null));
        Assert.Equal(ProjectRole.Admin, ProjectAccess.EffectiveRole(OrganizationRole.Admin, ProjectRole.Viewer));
        Assert.Equal(ProjectRole.Viewer, ProjectAccess.EffectiveRole(OrganizationRole.Member, ProjectRole.Viewer));
        Assert.Null(ProjectAccess.EffectiveRole(OrganizationRole.Member, projectRole: null));
    }

    [Fact]
    public void Each_project_role_adds_to_the_one_below_it()
    {
        ProjectRole? none = null;
        ProjectRole? viewer = ProjectRole.Viewer;
        ProjectRole? member = ProjectRole.Member;
        ProjectRole? admin = ProjectRole.Admin;

        Assert.False(none.CanRead());
        Assert.True(viewer.CanRead() && !viewer.CanWrite());
        Assert.True(member.CanWrite() && !member.CanManage());
        Assert.True(admin.CanManage());
    }

    [Fact]
    public void A_project_keeps_at_least_one_admin()
    {
        var project = Project.Create(s_organization, "ATL", "Atlas", null, s_user, s_now).Value;
        var admin = ProjectMember.Add(project, s_user, ProjectRole.Admin, s_now);

        Assert.Equal("projects.last_admin", admin.ChangeRole(ProjectRole.Member, adminCount: 1).Error.Code);
        Assert.Equal("projects.last_admin", admin.CanBeRemoved(adminCount: 1).Error.Code);
        Assert.True(admin.ChangeRole(ProjectRole.Member, adminCount: 2).IsSuccess);
    }

    [Fact]
    public void Archiving_keeps_the_first_archive_time_and_restoring_clears_it()
    {
        var project = Project.Create(s_organization, "ATL", "Atlas", null, s_user, s_now).Value;

        project.Archive(s_now);
        project.Archive(s_now.AddDays(1));
        Assert.Equal(s_now, project.ArchivedAt);

        project.Restore();
        Assert.False(project.IsArchived);
    }
}
