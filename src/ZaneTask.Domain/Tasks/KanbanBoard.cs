using ZaneTask.Domain.Common;

namespace ZaneTask.Domain.Tasks;

/// <summary>Keeps task positions contiguous (0..n-1) within each status column.</summary>
public static class KanbanBoard
{
    /// <summary>Next free position at the bottom of <paramref name="status"/>'s column.</summary>
    public static int NextPosition(IEnumerable<TaskItem> projectTasks, TaskItemStatus status) =>
        projectTasks.Count(t => t.Status == status);

    /// <summary>
    /// Moves <paramref name="task"/> to <paramref name="targetStatus"/> at <paramref name="targetPosition"/>
    /// and renumbers the affected columns. Positions past the end are clamped.
    /// </summary>
    /// <param name="projectTasks">All tasks of the task's project, including <paramref name="task"/>.</param>
    public static void Move(
        IReadOnlyCollection<TaskItem> projectTasks,
        TaskItem task,
        TaskItemStatus targetStatus,
        int targetPosition,
        DateTime now)
    {
        if (!projectTasks.Contains(task))
            throw new ArgumentException("Task must be part of the project's task list.", nameof(task));
        if (targetPosition < 0)
            throw new DomainException("Position cannot be negative.");

        var sourceStatus = task.Status;

        var target = Column(projectTasks, targetStatus).Where(t => t != task).ToList();
        target.Insert(Math.Min(targetPosition, target.Count), task);

        if (sourceStatus != targetStatus)
        {
            task.SetStatus(targetStatus, now);
            Renumber(Column(projectTasks, sourceStatus).Where(t => t != task));
        }

        Renumber(target);
    }

    /// <summary>Closes the gap left by a task that is about to be deleted.</summary>
    public static void Remove(IReadOnlyCollection<TaskItem> projectTasks, TaskItem task) =>
        Renumber(Column(projectTasks, task.Status).Where(t => t != task));

    private static IEnumerable<TaskItem> Column(IEnumerable<TaskItem> tasks, TaskItemStatus status) =>
        tasks.Where(t => t.Status == status).OrderBy(t => t.Position).ThenBy(t => t.CreatedAt);

    private static void Renumber(IEnumerable<TaskItem> column)
    {
        var index = 0;
        foreach (var t in column.ToList())
            t.Position = index++;
    }
}
