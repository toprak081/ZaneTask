using System.ComponentModel.DataAnnotations;

namespace ZaneTask.Contracts;

public enum TaskItemStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2,
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
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    UserDto? Assignee,
    int Position,
    UserDto CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<LabelDto> Labels,
    int CommentCount);

public sealed record CreateTaskRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(10000)] string? Description,
    TaskItemStatus Status = TaskItemStatus.Todo,
    TaskPriority Priority = TaskPriority.Medium,
    DateOnly? DueDate = null,
    Guid? AssigneeId = null,
    IReadOnlyList<Guid>? LabelIds = null);

public sealed record UpdateTaskRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(10000)] string? Description,
    TaskPriority Priority,
    DateOnly? DueDate);

public sealed record AssignTaskRequest(Guid? AssigneeId);

public sealed record MoveTaskRequest(
    TaskItemStatus Status,
    [Range(0, int.MaxValue)] int Position);

public sealed record CommentDto(
    Guid Id,
    Guid TaskId,
    UserDto Author,
    string Body,
    DateTime CreatedAt,
    DateTime? EditedAt);

public sealed record SaveCommentRequest([Required, MaxLength(4000)] string Body);
