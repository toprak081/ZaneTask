using ZaneTask.Domain.Common;

namespace ZaneTask.Domain.Tasks;

/// <summary>One step of a task's checklist. Created and changed through <see cref="TaskItem"/>.</summary>
public class ChecklistItem : Entity
{
    public const int TextMaxLength = 300;

    private ChecklistItem()
    {
        Text = null!;
    }

    internal ChecklistItem(Guid taskId, string text, int position)
    {
        TaskId = taskId;
        Text = Guard.Required(text, "Checklist item", TextMaxLength);
        Position = position;
    }

    public Guid TaskId { get; private set; }
    public string Text { get; private set; }
    public bool IsDone { get; private set; }

    /// <summary>Zero-based order inside the checklist.</summary>
    public int Position { get; internal set; }

    public DateTime? CompletedAt { get; private set; }

    internal void Rename(string text) => Text = Guard.Required(text, "Checklist item", TextMaxLength);

    internal void SetDone(bool done, DateTime now)
    {
        IsDone = done;
        CompletedAt = done ? now : null;
    }
}
