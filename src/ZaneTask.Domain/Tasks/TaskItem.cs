using ZaneTask.Domain.Common;
using ZaneTask.Domain.Projects;

namespace ZaneTask.Domain.Tasks;

public class TaskItem : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 10000;
    public const int ChecklistMaxItems = 100;

    private readonly List<Comment> _comments = [];
    private readonly List<Label> _labels = [];
    private readonly List<ChecklistItem> _checklist = [];

    private TaskItem()
    {
        Title = null!;
    }

    public Guid ProjectId { get; private set; }

    /// <summary>Per-project sequence number; shown with the project key, e.g. WEB-12.</summary>
    public int Number { get; private set; }

    public TaskType Type { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public Guid? AssigneeId { get; private set; }

    /// <summary>Zero-based order of the task inside its status column.</summary>
    public int Position { get; internal set; }

    public Guid CreatedById { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<Comment> Comments => _comments;
    public IReadOnlyCollection<Label> Labels => _labels;

    /// <summary>Checklist steps; load them before calling the checklist methods.</summary>
    public IReadOnlyCollection<ChecklistItem> Checklist => _checklist;

    /// <param name="project">Project the task belongs to; must include its members. Its task counter is advanced.</param>
    /// <param name="position">Position inside the status column; see <see cref="KanbanBoard.NextPosition"/>.</param>
    public static TaskItem Create(
        Project project,
        string title,
        string? description,
        TaskType type,
        TaskItemStatus status,
        TaskPriority priority,
        DateOnly? dueDate,
        Guid? assigneeId,
        Guid createdById,
        int position,
        DateTime now)
    {
        EnsureAssignable(project, assigneeId);

        return new TaskItem
        {
            ProjectId = project.Id,
            Number = project.AllocateTaskNumber(),
            Type = type,
            Title = Guard.Required(title, "Title", TitleMaxLength),
            Description = Guard.Optional(description, "Description", DescriptionMaxLength),
            Status = status,
            Priority = priority,
            DueDate = dueDate,
            AssigneeId = assigneeId,
            CreatedById = createdById,
            Position = position,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(string title, string? description, TaskType type, TaskPriority priority, DateOnly? dueDate, DateTime now)
    {
        Title = Guard.Required(title, "Title", TitleMaxLength);
        Type = type;
        Description = Guard.Optional(description, "Description", DescriptionMaxLength);
        Priority = priority;
        DueDate = dueDate;
        UpdatedAt = now;
    }

    /// <param name="project">The task's project; must include its members.</param>
    public void Assign(Project project, Guid? assigneeId, DateTime now)
    {
        if (project.Id != ProjectId)
            throw new DomainException("Task does not belong to this project.");
        EnsureAssignable(project, assigneeId);
        AssigneeId = assigneeId;
        UpdatedAt = now;
    }

    internal void SetStatus(TaskItemStatus status, DateTime now)
    {
        Status = status;
        UpdatedAt = now;
    }

    public Comment AddComment(Guid authorId, string body, DateTime now)
    {
        var comment = new Comment(Id, authorId, body, now);
        _comments.Add(comment);
        return comment;
    }

    public void AddLabel(Label label)
    {
        if (label.ProjectId != ProjectId)
            throw new DomainException("Label does not belong to this task's project.");
        if (_labels.All(l => l.Id != label.Id))
            _labels.Add(label);
    }

    public void RemoveLabel(Guid labelId) => _labels.RemoveAll(l => l.Id == labelId);

    public ChecklistItem AddChecklistItem(string text, DateTime now)
    {
        if (_checklist.Count >= ChecklistMaxItems)
            throw new DomainException($"A checklist can have at most {ChecklistMaxItems} items.");

        var item = new ChecklistItem(Id, text, _checklist.Count);
        _checklist.Add(item);
        UpdatedAt = now;
        return item;
    }

    public ChecklistItem RenameChecklistItem(Guid itemId, string text, DateTime now)
    {
        var item = GetChecklistItem(itemId);
        item.Rename(text);
        UpdatedAt = now;
        return item;
    }

    public ChecklistItem SetChecklistItemDone(Guid itemId, bool done, DateTime now)
    {
        var item = GetChecklistItem(itemId);
        if (item.IsDone != done)
        {
            item.SetDone(done, now);
            UpdatedAt = now;
        }
        return item;
    }

    /// <summary>Moves an item to <paramref name="position"/> (clamped) and renumbers the rest.</summary>
    public void MoveChecklistItem(Guid itemId, int position, DateTime now)
    {
        if (position < 0)
            throw new DomainException("Position cannot be negative.");

        var item = GetChecklistItem(itemId);
        var ordered = OrderedChecklist().Where(i => i != item).ToList();
        ordered.Insert(Math.Min(position, ordered.Count), item);
        Renumber(ordered);
        UpdatedAt = now;
    }

    public void RemoveChecklistItem(Guid itemId, DateTime now)
    {
        _checklist.Remove(GetChecklistItem(itemId));
        Renumber(OrderedChecklist().ToList());
        UpdatedAt = now;
    }

    private IEnumerable<ChecklistItem> OrderedChecklist() => _checklist.OrderBy(i => i.Position);

    private static void Renumber(List<ChecklistItem> items)
    {
        for (var i = 0; i < items.Count; i++)
            items[i].Position = i;
    }

    private ChecklistItem GetChecklistItem(Guid itemId) =>
        _checklist.FirstOrDefault(i => i.Id == itemId)
        ?? throw new DomainException("Checklist item does not belong to this task.");

    private static void EnsureAssignable(Project project, Guid? assigneeId)
    {
        if (assigneeId is { } id && !project.IsMember(id))
            throw new DomainException("Tasks can only be assigned to project members.");
    }
}
