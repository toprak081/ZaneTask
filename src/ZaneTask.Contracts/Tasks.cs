using System.ComponentModel.DataAnnotations;

namespace ZaneTask.Contracts;

public enum TaskItemStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2,
}

public enum TaskType
{
    Task = 0,
    Bug = 1,
    Feature = 2,
    Improvement = 3,
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

public sealed record TaskDto(
    Guid Id,
    Guid ProjectId,
    int Number,
    string Key,
    TaskType Type,
    string Title,
    string? Description,
    TaskItemStatus Status,
    Guid ColumnId,
    TaskPriority Priority,
    DateOnly? DueDate,
    UserDto? Assignee,
    int Position,
    UserDto CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<LabelDto> Labels,
    int CommentCount,
    int ChecklistDone,
    int ChecklistTotal);

/// <summary>A task assigned to the current user, with the name of the project it belongs to.</summary>
public sealed record MyTaskDto(TaskDto Task, string ProjectName);

public sealed record CreateTaskRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(10000)] string? Description,
    TaskItemStatus Status = TaskItemStatus.Todo,
    TaskPriority Priority = TaskPriority.Medium,
    DateOnly? DueDate = null,
    Guid? AssigneeId = null,
    IReadOnlyList<Guid>? LabelIds = null,
    TaskType Type = TaskType.Task,
    Guid? ColumnId = null);

public sealed record UpdateTaskRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(10000)] string? Description,
    TaskPriority Priority,
    DateOnly? DueDate,
    TaskType Type);

public sealed record AssignTaskRequest(Guid? AssigneeId);

/// <summary>
/// Target is <paramref name="ColumnId"/>, or when that is null the leftmost column of <paramref name="Category"/>
/// (e.g. "mark as done" without knowing the project's columns).
/// </summary>
public sealed record MoveTaskRequest(
    Guid? ColumnId,
    [Range(0, int.MaxValue)] int Position,
    TaskItemStatus? Category = null);

public sealed record CommentDto(
    Guid Id,
    Guid TaskId,
    UserDto Author,
    string Body,
    DateTime CreatedAt,
    DateTime? EditedAt);

public sealed record ChecklistItemDto(Guid Id, string Text, bool IsDone, int Position);

public sealed record AddChecklistItemRequest([Required, MaxLength(300)] string Text);

/// <summary>Null fields are left unchanged.</summary>
public sealed record UpdateChecklistItemRequest([MaxLength(300)] string? Text = null, bool? IsDone = null);

public sealed record MoveChecklistItemRequest([Range(0, int.MaxValue)] int Position);

public sealed record SaveCommentRequest([Required, MaxLength(4000)] string Body);
