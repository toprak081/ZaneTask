using ZaneTask.Application.Common;
using ZaneTask.Contracts;

namespace ZaneTask.Application.Tests;

public sealed class BoardColumnServiceTests : IDisposable
{
    private readonly TestDatabase _t = new();
    private readonly Guid _alice;
    private readonly Guid _bob;

    public BoardColumnServiceTests()
    {
        _alice = _t.AddUser("Alice");
        _bob = _t.AddUser("Bob");
        _t.ActAs(_alice);
    }

    public void Dispose() => _t.Dispose();

    private static BoardColumnDto Column(ProjectDto project, string name) => project.Columns.Single(c => c.Name == name);

    [Fact]
    public async Task Owner_builds_a_custom_workflow()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);

        project = await _t.Projects.CreateColumnAsync(project.Id, new("Review", TaskItemStatus.InProgress), default);
        project = await _t.Projects.MoveColumnAsync(project.Id, Column(project, "Review").Id, new(2), default);
        project = await _t.Projects.UpdateColumnAsync(project.Id, Column(project, "To do").Id, new("Backlog", TaskItemStatus.Todo), default);

        Assert.Equal(["Backlog", "In progress", "Review", "Done"], project.Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task Tasks_can_be_created_in_and_moved_to_a_specific_column()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        project = await _t.Projects.CreateColumnAsync(project.Id, new("Review", TaskItemStatus.InProgress), default);
        var review = Column(project, "Review");

        var task = await _t.Tasks.CreateAsync(project.Id, new("T", null, ColumnId: review.Id), default);
        Assert.Equal((review.Id, TaskItemStatus.InProgress), (task.ColumnId, task.Status));

        var done = await _t.Tasks.MoveAsync(task.Id, new(null, 0, TaskItemStatus.Done), default);
        Assert.Equal(Column(project, "Done").Id, done.ColumnId);
    }

    [Fact]
    public async Task Changing_a_column_meaning_updates_its_tasks()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        project = await _t.Projects.CreateColumnAsync(project.Id, new("Shipped", TaskItemStatus.InProgress), default);
        var shipped = Column(project, "Shipped");
        await _t.Tasks.CreateAsync(project.Id, new("Released feature", null, ColumnId: shipped.Id, AssigneeId: _alice), default);
        Assert.Single(await _t.Tasks.ListMineAsync(false, default));

        await _t.Projects.UpdateColumnAsync(project.Id, shipped.Id, new("Shipped", TaskItemStatus.Done), default);

        Assert.Empty(await _t.Tasks.ListMineAsync(false, default)); // now counts as done
        Assert.Equal(0, (await _t.Projects.ListAsync(default)).Single().OpenTaskCount);
    }

    [Fact]
    public async Task Deleting_a_column_moves_its_tasks_and_never_loses_them()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        var inProgress = Column(project, "In progress");
        var done = Column(project, "Done");
        await _t.Tasks.CreateAsync(project.Id, new("already done", null, ColumnId: done.Id), default);
        await _t.Tasks.CreateAsync(project.Id, new("a", null, ColumnId: inProgress.Id), default);
        await _t.Tasks.CreateAsync(project.Id, new("b", null, ColumnId: inProgress.Id), default);

        project = await _t.Projects.DeleteColumnAsync(project.Id, inProgress.Id, done.Id, default);

        Assert.Equal(["To do", "Done"], project.Columns.Select(c => c.Name));
        var board = await _t.Tasks.ListAsync(project.Id, new(), default);
        Assert.Equal(
            ["already done:0", "a:1", "b:2"],
            board.Where(t => t.ColumnId == done.Id).Select(t => $"{t.Title}:{t.Position}"));
        Assert.All(board, t => Assert.Equal(TaskItemStatus.Done, t.Status));
    }

    [Fact]
    public async Task Last_column_cannot_be_deleted_and_target_must_differ()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        var todo = Column(project, "To do");

        await Assert.ThrowsAsync<Domain.Common.DomainException>(
            () => _t.Projects.DeleteColumnAsync(project.Id, todo.Id, todo.Id, default));

        project = await _t.Projects.DeleteColumnAsync(project.Id, Column(project, "In progress").Id, null, default);
        project = await _t.Projects.DeleteColumnAsync(project.Id, Column(project, "Done").Id, null, default);
        await Assert.ThrowsAsync<Domain.Common.DomainException>(
            () => _t.Projects.DeleteColumnAsync(project.Id, todo.Id, null, default));
    }

    [Fact]
    public async Task Only_owners_change_columns()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        await _t.Projects.AddMemberAsync(project.Id, new("bob@example.com"), default);

        _t.ActAs(_bob);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _t.Projects.CreateColumnAsync(project.Id, new("Mine", TaskItemStatus.Todo), default));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _t.Projects.DeleteColumnAsync(project.Id, Column(project, "Done").Id, null, default));
    }
}
