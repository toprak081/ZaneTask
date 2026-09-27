using ZaneTask.Domain.Common;
using ZaneTask.Domain.Projects;

namespace ZaneTask.Domain.Tasks;

/// <summary>Keeps task positions contiguous (0..n-1) within each board column.</summary>
public static class KanbanBoard
{
    /// <summary>Next free position at the bottom of a column.</summary>
    public static int NextPosition(IEnumerable<TaskItem> projectTasks, Guid columnId) =>
        projectTasks.Count(t => t.ColumnId == columnId);

    /// <summary>
    /// Moves <paramref name="task"/> to <paramref name="target"/> at <paramref name="targetPosition"/>
    /// and renumbers the affected columns. Positions past the end are clamped.
    /// </summary>
    /// <param name="projectTasks">All tasks of the task's project, including <paramref name="task"/>.</param>
    public static void Move(
        IReadOnlyCollection<TaskItem> projectTasks,
        TaskItem task,
        BoardColumn target,
        int targetPosition,
        DateTime now)
    {
        if (!projectTasks.Contains(task))
            throw new ArgumentException("Task must be part of the project's task list.", nameof(task));
        if (target.ProjectId != task.ProjectId)
            throw new DomainException("Column does not belong to this task's project.");
        if (targetPosition < 0)
            throw new DomainException("Position cannot be negative.");

        var sourceColumnId = task.ColumnId;

        var column = Column(projectTasks, target.Id).Where(t => t != task).ToList();
        column.Insert(Math.Min(targetPosition, column.Count), task);

        if (sourceColumnId != target.Id)
        {
            task.MoveTo(target, now);
            Renumber(Column(projectTasks, sourceColumnId).Where(t => t != task));
        }

        Renumber(column);
    }

    /// <summary>Moves every task of one column to the bottom of another, keeping their order.</summary>
    public static void MoveAll(IReadOnlyCollection<TaskItem> projectTasks, Guid fromColumnId, BoardColumn target, DateTime now)
    {
        if (fromColumnId == target.Id)
            return;

        var position = NextPosition(projectTasks, target.Id);
        foreach (var task in Column(projectTasks, fromColumnId).ToList())
        {
            task.MoveTo(target, now);
            task.Position = position++;
        }
    }

    /// <summary>Closes the gap left by a task that is about to be deleted.</summary>
    public static void Remove(IReadOnlyCollection<TaskItem> projectTasks, TaskItem task) =>
        Renumber(Column(projectTasks, task.ColumnId).Where(t => t != task));

    private static IEnumerable<TaskItem> Column(IEnumerable<TaskItem> tasks, Guid columnId) =>
        tasks.Where(t => t.ColumnId == columnId).OrderBy(t => t.Position).ThenBy(t => t.CreatedAt);

    private static void Renumber(IEnumerable<TaskItem> column)
    {
        var index = 0;
        foreach (var t in column.ToList())
            t.Position = index++;
    }
}
