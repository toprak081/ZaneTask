using ZaneTask.Domain.Common;
using ZaneTask.Domain.Projects;

namespace ZaneTask.Domain.Tests;

public class ProjectTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _owner = Guid.NewGuid();

    [Fact]
    public void Create_makes_creator_the_owner()
    {
        var project = Project.Create("  Website  ", null, "PRJ", _owner, Now);

        Assert.Equal("Website", project.Name);
        var member = Assert.Single(project.Members);
        Assert.Equal(_owner, member.UserId);
        Assert.Equal(ProjectRole.Owner, member.Role);
        Assert.True(project.IsOwner(_owner));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_name(string name)
    {
        Assert.Throws<DomainException>(() => Project.Create(name, null, "PRJ", _owner, Now));
    }

    [Theory]
    [InlineData("web", "WEB")]
    [InlineData(" Mob2 ", "MOB2")]
    [InlineData("ab", "AB")]
    public void Key_is_trimmed_and_upper_cased(string key, string expected)
    {
        Assert.Equal(expected, Project.Create("P", null, key, _owner, Now).Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("W")]
    [InlineData("1WEB")]
    [InlineData("WEB-1")]
    [InlineData("TOOLONGKEY1")]
    public void Invalid_keys_are_rejected(string key)
    {
        Assert.Throws<DomainException>(() => Project.Create("P", null, key, _owner, Now));
        var project = Project.Create("P", null, "OK", _owner, Now);
        Assert.Throws<DomainException>(() => project.ChangeKey(key));
    }

    [Fact]
    public void Task_numbers_are_sequential_per_project()
    {
        var web = Project.Create("Web", null, "WEB", _owner, Now);
        var mob = Project.Create("Mobile", null, "MOB", _owner, Now);

        Assert.Equal([1, 2, 3], new[] { web.AllocateTaskNumber(), web.AllocateTaskNumber(), web.AllocateTaskNumber() });
        Assert.Equal(1, mob.AllocateTaskNumber());
        Assert.Equal(4, web.NextTaskNumber);
    }

    [Fact]
    public void AddMember_rejects_existing_member()
    {
        var project = Project.Create("P", null, "PRJ", _owner, Now);

        Assert.Throws<DomainException>(() => project.AddMember(_owner, ProjectRole.Member, Now));
    }

    [Fact]
    public void Last_owner_cannot_leave_or_be_demoted()
    {
        var project = Project.Create("P", null, "PRJ", _owner, Now);

        Assert.Throws<DomainException>(() => project.RemoveMember(_owner));
        Assert.Throws<DomainException>(() => project.ChangeMemberRole(_owner, ProjectRole.Member));
    }

    [Fact]
    public void Owner_can_be_demoted_when_another_owner_exists()
    {
        var project = Project.Create("P", null, "PRJ", _owner, Now);
        var other = Guid.NewGuid();
        project.AddMember(other, ProjectRole.Owner, Now);

        project.ChangeMemberRole(_owner, ProjectRole.Member);

        Assert.False(project.IsOwner(_owner));
        Assert.True(project.IsOwner(other));
    }

    [Fact]
    public void Label_names_are_unique_ignoring_case()
    {
        var project = Project.Create("P", null, "PRJ", _owner, Now);
        project.AddLabel("Bug", "#ff0000");

        Assert.Throws<DomainException>(() => project.AddLabel("bug", "#00ff00"));
    }

    [Fact]
    public void Renaming_a_label_to_another_labels_name_is_rejected()
    {
        var project = Project.Create("P", null, "PRJ", _owner, Now);
        project.AddLabel("Bug", "#ff0000");
        var feature = project.AddLabel("Feature", "#00ff00");

        Assert.Throws<DomainException>(() => project.UpdateLabel(feature.Id, "BUG", "#00ff00"));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#fff")]
    [InlineData("123456")]
    public void Label_color_must_be_six_digit_hex(string color)
    {
        var project = Project.Create("P", null, "PRJ", _owner, Now);

        Assert.Throws<DomainException>(() => project.AddLabel("Bug", color));
    }

    [Fact]
    public void Label_color_is_normalized_to_upper_case()
    {
        var project = Project.Create("P", null, "PRJ", _owner, Now);

        var label = project.AddLabel("Bug", "#1e88e5");

        Assert.Equal("#1E88E5", label.Color);
    }
}
