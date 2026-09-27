using ZaneTask.Domain.Common;

namespace ZaneTask.Domain.Tasks;

public enum TaskActivityKind
{
    Created = 0,
    TitleChanged = 1,
    DescriptionChanged = 2,
    TypeChanged = 3,
    PriorityChanged = 4,
    DueDateChanged = 5,
    AssigneeChanged = 6,
    Moved = 7,
    LabelAdded = 8,
    LabelRemoved = 9,
    ChecklistItemCompleted = 10,
    ChecklistItemReopened = 11,
}

/// <summary>
/// One entry of a task's history ("Alice moved the task from To do to Review"). Values are snapshots
/// (column and label names, dates, user ids) so the history stays readable after things are renamed or deleted.
/// </summary>
public class TaskActivity : Entity
{
    public const int ValueMaxLength = 300;

    private TaskActivity() { }

    public Guid TaskId { get; private set; }
    public Guid ActorId { get; private set; }
    public TaskActivityKind Kind { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime At { get; private set; }

    public static TaskActivity Create(
        Guid taskId, Guid actorId, TaskActivityKind kind, string? oldValue, string? newValue, DateTime at) => new()
    {
        TaskId = taskId,
        ActorId = actorId,
        Kind = kind,
        OldValue = Truncate(oldValue),
        NewValue = Truncate(newValue),
        At = at,
    };

    private static string? Truncate(string? value) =>
        value is { Length: > ValueMaxLength } ? value[..(ValueMaxLength - 1)] + "…" : value;
}
