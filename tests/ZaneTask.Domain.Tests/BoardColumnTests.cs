using ZaneTask.Domain.Common;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Domain.Tests;

public class BoardColumnTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private readonly Project _project = Project.Create("P", null, "PRJ", Guid.NewGuid(), Now);

    private string[] Names() => _project.OrderedColumns.Select(c => c.Name).ToArray();

    [Fact]
    public void New_projects_start_with_three_columns()
    {
        Assert.Equal(["To do", "In progress", "Done"], Names());
        Assert.Equal(
            [TaskItemStatus.Todo, TaskItemStatus.InProgress, TaskItemStatus.Done],
            _project.OrderedColumns.Select(c => c.Category));
    }

    [Fact]
    public void Columns_can_be_added_renamed_and_moved()
    {
        var review = _project.AddColumn("Review", TaskItemStatus.InProgress);
        _project.MoveColumn(review.Id, 2);
        _project.RenameColumn(review.Id, "Code review");

        Assert.Equal(["To do", "In progress", "Code review", "Done"], Names());
        Assert.Equal([0, 1, 2, 3], _project.OrderedColumns.Select(c => c.Position));
    }

    [Fact]
    public void Column_names_are_unique_ignoring_case()
    {
        Assert.Throws<DomainException>(() => _project.AddColumn("done", TaskItemStatus.Done));
        var todo = _project.DefaultColumn(TaskItemStatus.Todo);
        Assert.Throws<DomainException>(() => _project.RenameColumn(todo.Id, "DONE"));
    }

    [Fact]
    public void Board_keeps_at_least_one_and_at_most_twelve_columns()
    {
        for (var i = _project.Columns.Count; i < Project.MaxColumns; i++)
            _project.AddColumn($"Extra {i}", TaskItemStatus.InProgress);
        Assert.Throws<DomainException>(() => _project.AddColumn("One more", TaskItemStatus.Todo));

        foreach (var column in _project.OrderedColumns.Skip(1).ToList())
            _project.RemoveColumn(column.Id);
        Assert.Throws<DomainException>(() => _project.RemoveColumn(_project.Columns.Single().Id));
    }

    [Fact]
    public void Default_column_is_the_leftmost_of_a_category_with_a_fallback()
    {
        var qa = _project.AddColumn("QA", TaskItemStatus.Done);
        _project.MoveColumn(qa.Id, 0);

        Assert.Equal("QA", _project.DefaultColumn(TaskItemStatus.Done).Name);

        foreach (var todo in _project.Columns.Where(c => c.Category == TaskItemStatus.Todo).ToList())
            _project.RemoveColumn(todo.Id);
        Assert.Equal("QA", _project.DefaultColumn(TaskItemStatus.Todo).Name); // leftmost column
    }

    [Fact]
    public void MoveAll_appends_a_columns_tasks_to_another_and_keeps_their_order()
    {
        var owner = _project.Members.Single().UserId;
        var todo = _project.DefaultColumn(TaskItemStatus.Todo);
        var done = _project.DefaultColumn(TaskItemStatus.Done);
        var tasks = new List<TaskItem>();
        TaskItem Add(string title, BoardColumn column)
        {
            var t = TaskItem.Create(_project, title, null, TaskType.Task, column, TaskPriority.Medium, null, null, owner,
                KanbanBoard.NextPosition(tasks, column.Id), Now);
            tasks.Add(t);
            return t;
        }
        Add("d1", done);
        Add("a", todo);
        Add("b", todo);

        KanbanBoard.MoveAll(tasks, todo.Id, done, Now);

        Assert.Equal(["d1", "a", "b"], tasks.Where(t => t.ColumnId == done.Id).OrderBy(t => t.Position).Select(t => t.Title));
        Assert.All(tasks, t => Assert.Equal(TaskItemStatus.Done, t.Status));
    }
}
