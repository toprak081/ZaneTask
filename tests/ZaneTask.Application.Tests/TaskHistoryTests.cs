using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;

namespace ZaneTask.Application.Tests;

public sealed class TaskHistoryTests : IDisposable
{
    private readonly TestDatabase _t = new();
    private readonly Guid _alice;
    private readonly Guid _bob;

    public TaskHistoryTests()
    {
        _alice = _t.AddUser("Alice");
        _bob = _t.AddUser("Bob");
        _t.ActAs(_alice);
    }

    public void Dispose() => _t.Dispose();

    private async Task<(ProjectDto Project, TaskDto Task)> NewTaskAsync()
    {
        var project = await _t.Projects.CreateAsync(new("P", null), default);
        await _t.Projects.AddMemberAsync(project.Id, new("bob@example.com"), default);
        project = await _t.Projects.GetAsync(project.Id, default);
        var task = await _t.Tasks.CreateAsync(project.Id, new("Draft", null), default);
        return (project, task);
    }

    /// <summary>History oldest first, as "Kind:old->new".</summary>
    private async Task<string[]> HistoryAsync(Guid taskId) =>
        (await _t.Tasks.ListActivityAsync(taskId, default))
            .Reverse()
            .Select(a => $"{a.Kind}:{a.OldValue}->{a.NewValue}")
            .ToArray();

    [Fact]
    public async Task Creation_and_each_changed_field_are_recorded_once()
    {
        var (_, task) = await NewTaskAsync();

        await _t.Tasks.UpdateAsync(task.Id, new("Final", "Details", TaskPriority.High, new DateOnly(2026, 10, 1), TaskType.Bug), default);
        // Saving again without changes records nothing new.
        await _t.Tasks.UpdateAsync(task.Id, new("Final", "Details", TaskPriority.High, new DateOnly(2026, 10, 1), TaskType.Bug), default);

        Assert.Equal(
            [
                "Created:->",
                "TitleChanged:Draft->Final",
                "DescriptionChanged:->",
                "TypeChanged:Task->Bug",
                "PriorityChanged:Medium->High",
                "DueDateChanged:->2026-10-01",
            ],
            await HistoryAsync(task.Id));
    }

    [Fact]
    public async Task Assignments_moves_labels_and_checklist_are_recorded()
    {
        var (project, task) = await NewTaskAsync();
        var bug = await _t.Projects.CreateLabelAsync(project.Id, new("Bug", "#FF0000"), default);
        var review = (await _t.Projects.CreateColumnAsync(project.Id, new("Review", TaskItemStatus.InProgress), default))
            .Columns.Single(c => c.Name == "Review");

        await _t.Tasks.AssignAsync(task.Id, new(_bob), default);
        await _t.Tasks.AssignAsync(task.Id, new(_alice), default);
        await _t.Tasks.AssignAsync(task.Id, new(null), default);
        await _t.Tasks.MoveAsync(task.Id, new(review.Id, 0), default);
        await _t.Tasks.MoveAsync(task.Id, new(review.Id, 0), default); // same column: not history
        await _t.Tasks.AddLabelAsync(task.Id, bug.Id, default);
        await _t.Tasks.AddLabelAsync(task.Id, bug.Id, default); // already there: not history
        await _t.Tasks.RemoveLabelAsync(task.Id, bug.Id, default);
        var items = await _t.Checklist.AddAsync(task.Id, new("Write tests"), default);
        await _t.Checklist.UpdateAsync(items[0].Id, new(IsDone: true), default);
        await _t.Checklist.UpdateAsync(items[0].Id, new(IsDone: false), default);

        Assert.Equal(
            [
                "Created:->",
                "AssigneeChanged:->Bob",
                "AssigneeChanged:Bob->Alice",
                "AssigneeChanged:Alice->",
                "Moved:To do->Review",
                "LabelAdded:->Bug",
                "LabelRemoved:Bug->",
                "ChecklistItemCompleted:->Write tests",
                "ChecklistItemReopened:->Write tests",
            ],
            await HistoryAsync(task.Id));

        var newest = (await _t.Tasks.ListActivityAsync(task.Id, default))[0];
        Assert.Equal("Alice", newest.Actor.DisplayName);
    }

    [Fact]
    public async Task Deleting_a_column_and_removing_a_member_leave_a_trace()
    {
        var (project, task) = await NewTaskAsync();
        await _t.Tasks.AssignAsync(task.Id, new(_bob), default);

        await _t.Projects.DeleteColumnAsync(project.Id, project.Columns.Single(c => c.Name == "To do").Id,
            project.Columns.Single(c => c.Name == "Done").Id, default);
        await _t.Projects.RemoveMemberAsync(project.Id, _bob, default);

        var history = await HistoryAsync(task.Id);
        Assert.Contains("Moved:To do->Done", history);
        Assert.Contains("AssigneeChanged:Bob->", history);
    }

    [Fact]
    public async Task History_is_private_to_members_and_deleted_with_the_task()
    {
        var (_, task) = await NewTaskAsync();
        var carol = _t.AddUser("Carol");

        _t.ActAs(carol);
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tasks.ListActivityAsync(task.Id, default));

        _t.ActAs(_alice);
        await _t.Tasks.DeleteAsync(task.Id, default);
        await using var db = _t.NewContext();
        Assert.False(await db.TaskActivities.AnyAsync());
    }
}
