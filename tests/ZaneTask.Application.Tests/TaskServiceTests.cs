using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Common;
using ZaneTask.Application.Tasks;
using ZaneTask.Contracts;
using ZaneTask.Domain.Common;

namespace ZaneTask.Application.Tests;

public sealed class TaskServiceTests : IDisposable
{
    private readonly TestDatabase _t = new();
    private readonly Guid _alice;
    private readonly Guid _bob;
    private Guid _projectId;

    public TaskServiceTests()
    {
        _alice = _t.AddUser("Alice");
        _bob = _t.AddUser("Bob");
        _t.ActAs(_alice);
    }

    public void Dispose() => _t.Dispose();

    private async Task<Guid> ProjectWithBobAsync()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        await _t.Projects.AddMemberAsync(project.Id, new("bob@example.com"), default);
        _projectId = project.Id;
        return project.Id;
    }

    private Task<TaskDto> CreateAsync(string title, TaskItemStatus status = TaskItemStatus.Todo) =>
        _t.Tasks.CreateAsync(_projectId, new(title, null, Status: status), default);

    [Fact]
    public async Task Create_assigns_next_position_in_column()
    {
        await ProjectWithBobAsync();

        var a = await CreateAsync("a");
        var b = await CreateAsync("b");
        var x = await CreateAsync("x", TaskItemStatus.Done);

        Assert.Equal([0, 1, 0], new[] { a.Position, b.Position, x.Position });
        Assert.Equal("Alice", a.CreatedBy.DisplayName);
    }

    [Fact]
    public async Task Create_with_assignee_and_labels()
    {
        await ProjectWithBobAsync();
        var bug = await _t.Projects.CreateLabelAsync(_projectId, new("Bug", "#FF0000"), default);

        var task = await _t.Tasks.CreateAsync(
            _projectId,
            new("Fix login", "Steps...", TaskItemStatus.InProgress, TaskPriority.High, new DateOnly(2026, 10, 1), _bob, [bug.Id]),
            default);

        Assert.Equal("Bob", task.Assignee?.DisplayName);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(new DateOnly(2026, 10, 1), task.DueDate);
        Assert.Equal([bug], task.Labels);
    }

    [Fact]
    public async Task Cannot_assign_to_non_member()
    {
        await ProjectWithBobAsync();
        var carol = _t.AddUser("Carol");
        var task = await CreateAsync("a");

        await Assert.ThrowsAsync<DomainException>(() => _t.Tasks.AssignAsync(task.Id, new(carol), default));
    }

    [Fact]
    public async Task Move_is_persisted_with_contiguous_positions()
    {
        await ProjectWithBobAsync();
        var a = await CreateAsync("a");
        var b = await CreateAsync("b");
        await CreateAsync("c");
        await CreateAsync("x", TaskItemStatus.InProgress);

        _t.ActAs(_bob);
        var moved = await _t.Tasks.MoveAsync(b.Id, new(TaskItemStatus.InProgress, 0), default);
        Assert.Equal(TaskItemStatus.InProgress, moved.Status);

        _t.ActAs(_alice);
        var board = await _t.Tasks.ListAsync(_projectId, new(), default);
        Assert.Equal(
            ["a:Todo:0", "c:Todo:1", "b:InProgress:0", "x:InProgress:1"],
            board.Select(t => $"{t.Title}:{t.Status}:{t.Position}"));
        Assert.Equal(a.Id, board[0].Id);
    }

    [Fact]
    public async Task Delete_closes_the_gap_and_is_limited_to_creator_or_owner()
    {
        await ProjectWithBobAsync();
        await CreateAsync("a");
        var b = await CreateAsync("b");
        await CreateAsync("c");

        _t.ActAs(_bob);
        await Assert.ThrowsAsync<ForbiddenException>(() => _t.Tasks.DeleteAsync(b.Id, default));

        _t.ActAs(_alice);
        await _t.Tasks.DeleteAsync(b.Id, default);

        var board = await _t.Tasks.ListAsync(_projectId, new(), default);
        Assert.Equal(["a:0", "c:1"], board.Select(t => $"{t.Title}:{t.Position}"));
    }

    [Fact]
    public async Task List_filters_by_status_assignee_label_and_search()
    {
        await ProjectWithBobAsync();
        var bug = await _t.Projects.CreateLabelAsync(_projectId, new("Bug", "#FF0000"), default);
        await _t.Tasks.CreateAsync(_projectId, new("Fix Login page", null, AssigneeId: _bob, LabelIds: [bug.Id]), default);
        await _t.Tasks.CreateAsync(_projectId, new("Write docs", "about login", Status: TaskItemStatus.Done), default);
        await _t.Tasks.CreateAsync(_projectId, new("Deploy", null), default);

        async Task<string[]> Titles(TaskFilter f) =>
            (await _t.Tasks.ListAsync(_projectId, f, default)).Select(t => t.Title).ToArray();

        Assert.Equal(["Write docs"], await Titles(new(Status: TaskItemStatus.Done)));
        Assert.Equal(["Fix Login page"], await Titles(new(AssigneeId: _bob)));
        Assert.Equal(["Fix Login page"], await Titles(new(LabelId: bug.Id)));
        Assert.Equal(["Fix Login page", "Write docs"], await Titles(new(Search: "LOGIN")));
    }

    [Fact]
    public async Task My_tasks_lists_only_my_open_assignments_ordered_by_due_date_then_priority()
    {
        await ProjectWithBobAsync();
        var other = await _t.Projects.CreateAsync(new("Other", null), default);

        await _t.Tasks.CreateAsync(_projectId, new("No date, low", null, Priority: TaskPriority.Low, AssigneeId: _alice), default);
        await _t.Tasks.CreateAsync(_projectId, new("Later", null, DueDate: new DateOnly(2026, 12, 1), AssigneeId: _alice), default);
        await _t.Tasks.CreateAsync(other.Id, new("Soon, other project", null, DueDate: new DateOnly(2026, 10, 1), AssigneeId: _alice), default);
        await _t.Tasks.CreateAsync(_projectId, new("No date, critical", null, Priority: TaskPriority.Critical, AssigneeId: _alice), default);
        await _t.Tasks.CreateAsync(_projectId, new("Finished", null, Status: TaskItemStatus.Done, AssigneeId: _alice), default);
        await _t.Tasks.CreateAsync(_projectId, new("Bob's", null, AssigneeId: _bob), default);
        await _t.Tasks.CreateAsync(_projectId, new("Unassigned", null), default);

        var open = await _t.Tasks.ListMineAsync(includeDone: false, default);
        Assert.Equal(
            ["Soon, other project", "Later", "No date, critical", "No date, low"],
            open.Select(t => t.Task.Title));
        Assert.Equal("Other", open[0].ProjectName);
        Assert.Equal("P", open[1].ProjectName);

        var all = await _t.Tasks.ListMineAsync(includeDone: true, default);
        Assert.Contains(all, t => t.Task.Title == "Finished");
    }

    [Fact]
    public async Task My_tasks_hides_projects_I_left()
    {
        await ProjectWithBobAsync();
        await _t.Tasks.CreateAsync(_projectId, new("For Bob", null, AssigneeId: _bob), default);

        _t.ActAs(_bob);
        Assert.Single(await _t.Tasks.ListMineAsync(false, default));

        await _t.Projects.RemoveMemberAsync(_projectId, _bob, default);
        Assert.Empty(await _t.Tasks.ListMineAsync(false, default));
    }

    [Fact]
    public async Task Labels_can_be_added_and_removed_on_a_task()
    {
        await ProjectWithBobAsync();
        var bug = await _t.Projects.CreateLabelAsync(_projectId, new("Bug", "#FF0000"), default);
        var task = await CreateAsync("a");

        var withLabel = await _t.Tasks.AddLabelAsync(task.Id, bug.Id, default);
        Assert.Equal([bug], withLabel.Labels);

        _t.ActAs(_alice);
        var withoutLabel = await _t.Tasks.RemoveLabelAsync(task.Id, bug.Id, default);
        Assert.Empty(withoutLabel.Labels);

        await using var db = _t.NewContext();
        Assert.Empty((await db.Tasks.Include(t => t.Labels).SingleAsync()).Labels);
    }

    [Fact]
    public async Task Comments_permissions()
    {
        await ProjectWithBobAsync();
        var task = await CreateAsync("a");

        _t.ActAs(_bob);
        var comment = await _t.Comments.AddAsync(task.Id, new("Bob was here"), default);
        Assert.Equal("Bob", comment.Author.DisplayName);

        _t.ActAs(_alice);
        await Assert.ThrowsAsync<ForbiddenException>(() => _t.Comments.UpdateAsync(comment.Id, new("edited"), default));

        _t.ActAs(_bob);
        var edited = await _t.Comments.UpdateAsync(comment.Id, new("Bob edited"), default);
        Assert.NotNull(edited.EditedAt);

        _t.ActAs(_alice);
        await _t.Comments.DeleteAsync(comment.Id, default); // owner may delete anyone's comment
        Assert.Empty(await _t.Comments.ListAsync(task.Id, default));
        Assert.Equal(0, (await _t.Tasks.GetAsync(task.Id, default)).CommentCount);
    }
}
