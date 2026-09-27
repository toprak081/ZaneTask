using ZaneTask.Domain.Common;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Domain.Tests;

public class ChecklistTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private readonly TaskItem _task;

    public ChecklistTests()
    {
        var owner = Guid.NewGuid();
        var project = Project.Create("P", null, "PRJ", owner, Now);
        _task = TaskItem.Create(project, "T", null, TaskType.Task, TaskItemStatus.Todo, TaskPriority.Medium, null, null, owner, 0, Now);
    }

    private string[] Texts() => _task.Checklist.OrderBy(i => i.Position).Select(i => i.Text).ToArray();

    [Fact]
    public void Items_are_appended_in_order()
    {
        _task.AddChecklistItem("a", Now);
        _task.AddChecklistItem(" b ", Now);

        Assert.Equal(["a", "b"], Texts());
        Assert.Equal([0, 1], _task.Checklist.OrderBy(i => i.Position).Select(i => i.Position));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_items_are_rejected(string text)
    {
        Assert.Throws<DomainException>(() => _task.AddChecklistItem(text, Now));
    }

    [Fact]
    public void Checklist_is_limited_to_100_items()
    {
        for (var i = 0; i < TaskItem.ChecklistMaxItems; i++)
            _task.AddChecklistItem($"item {i}", Now);

        Assert.Throws<DomainException>(() => _task.AddChecklistItem("one too many", Now));
    }

    [Fact]
    public void Completing_records_when_and_reopening_clears_it()
    {
        var item = _task.AddChecklistItem("a", Now);

        _task.SetChecklistItemDone(item.Id, true, Now.AddHours(1));
        Assert.True(item.IsDone);
        Assert.Equal(Now.AddHours(1), item.CompletedAt);

        _task.SetChecklistItemDone(item.Id, false, Now.AddHours(2));
        Assert.False(item.IsDone);
        Assert.Null(item.CompletedAt);
    }

    [Fact]
    public void Move_reorders_and_clamps()
    {
        _task.AddChecklistItem("a", Now);
        _task.AddChecklistItem("b", Now);
        var c = _task.AddChecklistItem("c", Now);

        _task.MoveChecklistItem(c.Id, 0, Now);
        Assert.Equal(["c", "a", "b"], Texts());

        _task.MoveChecklistItem(c.Id, 99, Now);
        Assert.Equal(["a", "b", "c"], Texts());
    }

    [Fact]
    public void Remove_closes_the_gap()
    {
        _task.AddChecklistItem("a", Now);
        var b = _task.AddChecklistItem("b", Now);
        _task.AddChecklistItem("c", Now);

        _task.RemoveChecklistItem(b.Id, Now);

        Assert.Equal(["a", "c"], Texts());
        Assert.Equal([0, 1], _task.Checklist.OrderBy(i => i.Position).Select(i => i.Position));
    }

    [Fact]
    public void Items_of_another_task_are_rejected()
    {
        Assert.Throws<DomainException>(() => _task.SetChecklistItemDone(Guid.NewGuid(), true, Now));
    }
}
