using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;

namespace ZaneTask.Application.Tests;

public sealed class ChecklistServiceTests : IDisposable
{
    private readonly TestDatabase _t = new();
    private readonly Guid _alice;
    private readonly Guid _bob;

    public ChecklistServiceTests()
    {
        _alice = _t.AddUser("Alice");
        _bob = _t.AddUser("Bob");
        _t.ActAs(_alice);
    }

    public void Dispose() => _t.Dispose();

    private async Task<TaskDto> NewTaskAsync()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        return await _t.Tasks.CreateAsync(project.Id, new("T", null), default);
    }

    [Fact]
    public async Task Full_lifecycle_is_persisted_and_counted_on_the_task()
    {
        var task = await NewTaskAsync();

        await _t.Checklist.AddAsync(task.Id, new("Design"), default);
        await _t.Checklist.AddAsync(task.Id, new("Build"), default);
        var items = await _t.Checklist.AddAsync(task.Id, new("Ship"), default);

        items = await _t.Checklist.UpdateAsync(items[0].Id, new(IsDone: true), default);
        items = await _t.Checklist.UpdateAsync(items[1].Id, new(Text: "Build it"), default);
        items = await _t.Checklist.MoveAsync(items[2].Id, new(0), default);
        Assert.Equal(["Ship", "Design", "Build it"], items.Select(i => i.Text));

        items = await _t.Checklist.DeleteAsync(items[0].Id, default);
        Assert.Equal([0, 1], items.Select(i => i.Position));

        _t.ActAs(_alice); // fresh change tracker
        var persisted = await _t.Checklist.ListAsync(task.Id, default);
        Assert.Equal([("Design", true), ("Build it", false)], persisted.Select(i => (i.Text, i.IsDone)));

        var dto = await _t.Tasks.GetAsync(task.Id, default);
        Assert.Equal((1, 2), (dto.ChecklistDone, dto.ChecklistTotal));
        var board = await _t.Tasks.ListAsync(task.ProjectId, new(), default);
        Assert.Equal((1, 2), (board[0].ChecklistDone, board[0].ChecklistTotal));
    }

    [Fact]
    public async Task Non_members_cannot_see_or_change_a_checklist()
    {
        var task = await NewTaskAsync();
        var items = await _t.Checklist.AddAsync(task.Id, new("Secret step"), default);

        _t.ActAs(_bob);

        await Assert.ThrowsAsync<NotFoundException>(() => _t.Checklist.ListAsync(task.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Checklist.AddAsync(task.Id, new("x"), default));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Checklist.UpdateAsync(items[0].Id, new(IsDone: true), default));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Checklist.DeleteAsync(items[0].Id, default));
    }

    [Fact]
    public async Task Deleting_a_task_deletes_its_checklist()
    {
        var task = await NewTaskAsync();
        await _t.Checklist.AddAsync(task.Id, new("a"), default);

        await _t.Tasks.DeleteAsync(task.Id, default);

        await using var db = _t.NewContext();
        Assert.False(await db.Set<Domain.Tasks.ChecklistItem>().AnyAsync());
    }
}
