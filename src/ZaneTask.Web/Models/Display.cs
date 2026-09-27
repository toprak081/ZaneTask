using ZaneTask.Contracts;

namespace ZaneTask.Web.Models;

/// <summary>Human-readable names and icons for enum values; status and priority never rely on color alone.</summary>
public static class Display
{
    public static readonly TaskItemStatus[] Statuses = [TaskItemStatus.Todo, TaskItemStatus.InProgress, TaskItemStatus.Done];
    public static readonly TaskPriority[] Priorities = [TaskPriority.Low, TaskPriority.Medium, TaskPriority.High, TaskPriority.Critical];
    public static readonly TaskType[] Types = [TaskType.Task, TaskType.Bug, TaskType.Feature, TaskType.Improvement];

    public static string Name(this TaskItemStatus status) => status switch
    {
        TaskItemStatus.Todo => "To do",
        TaskItemStatus.InProgress => "In progress",
        TaskItemStatus.Done => "Done",
        _ => status.ToString(),
    };

    public static string Icon(this TaskItemStatus status) => status switch
    {
        TaskItemStatus.InProgress => "circle-half",
        TaskItemStatus.Done => "circle-check",
        _ => "circle",
    };

    public static string Name(this TaskPriority priority) => priority.ToString();

    public static string Name(this TaskType type) => type.ToString();

    public static string Icon(this TaskType type) => type switch
    {
        TaskType.Bug => "bug",
        TaskType.Feature => "sparkles",
        TaskType.Improvement => "wrench",
        _ => "square-check",
    };

    public static string CssClass(this TaskPriority priority) => $"priority--{priority.ToString().ToLowerInvariant()}";

    public static string Name(this ProjectRole role) => role == ProjectRole.Owner ? "Owner" : "Member";

    public static string Initials(this UserDto user)
    {
        var parts = user.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant(),
        };
    }

    public static string DueText(DateOnly due, DateOnly today)
    {
        var days = due.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "Due today",
            1 => "Due tomorrow",
            -1 => "Due yesterday",
            < 0 => $"Overdue · {due:MMM d}",
            _ => $"Due {due:MMM d}",
        };
    }
}
