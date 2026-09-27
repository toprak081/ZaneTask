using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;

namespace ZaneTask.Application.Tests;

public sealed class ProjectServiceTests : IDisposable
{
    private readonly TestDatabase _t = new();
    private readonly Guid _alice;
    private readonly Guid _bob;

    public ProjectServiceTests()
    {
        _alice = _t.AddUser("Alice");
        _bob = _t.AddUser("Bob");
        _t.ActAs(_alice);
    }

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task List_returns_only_my_projects_with_counts()
    {
        var mine = await _t.Projects.CreateAsync(new("Mine", null), default);
        await _t.Tasks.CreateAsync(mine.Id, new("Open", null), default);
        await _t.Tasks.CreateAsync(mine.Id, new("Closed", null, Status: TaskItemStatus.Done), default);

        _t.ActAs(_bob);
        await _t.Projects.CreateAsync(new("Bob's", null), default);

        _t.ActAs(_alice);
        var list = await _t.Projects.ListAsync(default);

        var summary = Assert.Single(list);
        Assert.Equal("Mine", summary.Name);
        Assert.Equal(ProjectRole.Owner, summary.MyRole);
        Assert.Equal(1, summary.MemberCount);
        Assert.Equal(1, summary.OpenTaskCount);
    }

    [Fact]
    public async Task Non_members_get_not_found()
    {
        var project = await _t.Projects.CreateAsync(new("Secret", null), default);

        _t.ActAs(_bob);

        await Assert.ThrowsAsync<NotFoundException>(() => _t.Projects.GetAsync(project.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tasks.ListAsync(project.Id, new(), default));
    }

    [Fact]
    public async Task Owner_adds_member_by_email()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);

        var member = await _t.Projects.AddMemberAsync(project.Id, new("BOB@example.com"), default);

        Assert.Equal(_bob, member.User.Id);
        Assert.Equal(ProjectRole.Member, member.Role);

        _t.ActAs(_bob);
        var seenByBob = await _t.Projects.GetAsync(project.Id, default);
        Assert.Equal(ProjectRole.Member, seenByBob.MyRole);
        Assert.Equal(2, seenByBob.Members.Count);
    }

    [Fact]
    public async Task Adding_unknown_email_is_not_found()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _t.Projects.AddMemberAsync(project.Id, new("nobody@example.com"), default));
    }

    [Fact]
    public async Task Members_cannot_manage_members_or_settings()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        await _t.Projects.AddMemberAsync(project.Id, new("bob@example.com"), default);
        _t.AddUser("Carol");

        _t.ActAs(_bob);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _t.Projects.AddMemberAsync(project.Id, new("carol@example.com"), default));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _t.Projects.UpdateAsync(project.Id, new("Renamed", null), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => _t.Projects.RemoveMemberAsync(project.Id, _alice, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => _t.Projects.DeleteAsync(project.Id, default));
    }

    [Fact]
    public async Task Member_can_leave_and_their_tasks_are_unassigned()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        await _t.Projects.AddMemberAsync(project.Id, new("bob@example.com"), default);
        var task = await _t.Tasks.CreateAsync(project.Id, new("Bob's task", null, AssigneeId: _bob), default);

        _t.ActAs(_bob);
        await _t.Projects.RemoveMemberAsync(project.Id, _bob, default);

        await using var db = _t.NewContext();
        Assert.False(await db.Set<Domain.Projects.ProjectMember>().AnyAsync(m => m.UserId == _bob));
        Assert.Null((await db.Tasks.SingleAsync(x => x.Id == task.Id)).AssigneeId);
    }

    [Fact]
    public async Task Deleting_a_project_deletes_its_tasks()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        var task = await _t.Tasks.CreateAsync(project.Id, new("T", null), default);
        await _t.Comments.AddAsync(task.Id, new("hi"), default);

        await _t.Projects.DeleteAsync(project.Id, default);

        await using var db = _t.NewContext();
        Assert.False(await db.Tasks.AnyAsync());
        Assert.False(await db.Comments.AnyAsync());
    }

    [Fact]
    public async Task Labels_can_be_created_renamed_and_deleted()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);

        var label = await _t.Projects.CreateLabelAsync(project.Id, new("Bug", "#ff0000"), default);
        var renamed = await _t.Projects.UpdateLabelAsync(project.Id, label.Id, new("Defect", "#00ff00"), default);
        Assert.Equal(new LabelDto(label.Id, "Defect", "#00FF00"), renamed);

        await _t.Projects.DeleteLabelAsync(project.Id, label.Id, default);
        Assert.Empty((await _t.Projects.GetAsync(project.Id, default)).Labels);
    }
}
