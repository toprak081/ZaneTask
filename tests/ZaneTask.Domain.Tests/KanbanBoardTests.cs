using ZaneTask.Domain.Common;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Domain.Tests;

public class KanbanBoardTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Project _project;
    private readonly List<TaskItem> _tasks = [];

    public KanbanBoardTests()
    {
        _project = Project.Create("P", null, "PRJ", _owner, Now);
    }

    private TaskItem Add(string title, TaskItemStatus status)
    {
        var task = TaskItem.Create(
            _project, title, null, TaskType.Task, status, TaskPriority.Medium, null, null, _owner,
            KanbanBoard.NextPosition(_tasks, status), Now);
        _tasks.Add(task);
        return task;
    }

    private string[] Column(TaskItemStatus status) =>
        _tasks.Where(t => t.Status == status).OrderBy(t => t.Position).Select(t => t.Title).ToArray();

    private void AssertPositionsContiguous()
    {
        foreach (var group in _tasks.GroupBy(t => t.Status))
            Assert.Equal(Enumerable.Range(0, group.Count()), group.Select(t => t.Position).Order());
    }

    [Fact]
    public void NextPosition_appends_to_the_column()
    {
        Add("a", TaskItemStatus.Todo);
        Add("b", TaskItemStatus.Todo);
        var c = Add("c", TaskItemStatus.Done);

        Assert.Equal(2, KanbanBoard.NextPosition(_tasks, TaskItemStatus.Todo));
        Assert.Equal(0, c.Position);
    }

    [Fact]
    public void Move_within_column_reorders()
    {
        Add("a", TaskItemStatus.Todo);
        Add("b", TaskItemStatus.Todo);
        var c = Add("c", TaskItemStatus.Todo);

        KanbanBoard.Move(_tasks, c, TaskItemStatus.Todo, 0, Now);

        Assert.Equal(["c", "a", "b"], Column(TaskItemStatus.Todo));
        AssertPositionsContiguous();
    }

    [Fact]
    public void Move_across_columns_renumbers_both()
    {
        Add("a", TaskItemStatus.Todo);
        var b = Add("b", TaskItemStatus.Todo);
        Add("c", TaskItemStatus.Todo);
        Add("x", TaskItemStatus.InProgress);
        Add("y", TaskItemStatus.InProgress);

        KanbanBoard.Move(_tasks, b, TaskItemStatus.InProgress, 1, Now.AddMinutes(1));

        Assert.Equal(["a", "c"], Column(TaskItemStatus.Todo));
        Assert.Equal(["x", "b", "y"], Column(TaskItemStatus.InProgress));
        Assert.Equal(TaskItemStatus.InProgress, b.Status);
        Assert.Equal(Now.AddMinutes(1), b.UpdatedAt);
        AssertPositionsContiguous();
    }

    [Fact]
    public void Move_past_the_end_appends()
    {
        var a = Add("a", TaskItemStatus.Todo);
        Add("x", TaskItemStatus.Done);

        KanbanBoard.Move(_tasks, a, TaskItemStatus.Done, 99, Now);

        Assert.Equal(["x", "a"], Column(TaskItemStatus.Done));
        Assert.Empty(Column(TaskItemStatus.Todo));
        AssertPositionsContiguous();
    }

    [Fact]
    public void Move_to_negative_position_is_rejected()
    {
        var a = Add("a", TaskItemStatus.Todo);

        Assert.Throws<DomainException>(() => KanbanBoard.Move(_tasks, a, TaskItemStatus.Todo, -1, Now));
    }

    [Fact]
    public void Remove_closes_the_gap()
    {
        Add("a", TaskItemStatus.Todo);
        var b = Add("b", TaskItemStatus.Todo);
        Add("c", TaskItemStatus.Todo);

        KanbanBoard.Remove(_tasks, b);
        _tasks.Remove(b);

        Assert.Equal(["a", "c"], Column(TaskItemStatus.Todo));
        AssertPositionsContiguous();
    }
}
