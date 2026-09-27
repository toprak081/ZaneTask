using ZaneTask.Domain.Common;
using Dto = ZaneTask.Contracts;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Application.Common;

/// <summary>Conversions between domain enums and their contract twins.</summary>
internal static class Mapping
{
    public static ProjectRole ToDomain(this Dto.ProjectRole value) => Convert<Dto.ProjectRole, ProjectRole>(value);
    public static TaskItemStatus ToDomain(this Dto.TaskItemStatus value) => Convert<Dto.TaskItemStatus, TaskItemStatus>(value);
    public static TaskPriority ToDomain(this Dto.TaskPriority value) => Convert<Dto.TaskPriority, TaskPriority>(value);
    public static TaskType ToDomain(this Dto.TaskType value) => Convert<Dto.TaskType, TaskType>(value);

    public static Dto.ProjectRole ToDto(this ProjectRole value) => (Dto.ProjectRole)(int)value;
    public static Dto.TaskItemStatus ToDto(this TaskItemStatus value) => (Dto.TaskItemStatus)(int)value;
    public static Dto.TaskPriority ToDto(this TaskPriority value) => (Dto.TaskPriority)(int)value;
    public static Dto.TaskType ToDto(this TaskType value) => (Dto.TaskType)(int)value;

    public static Dto.LabelDto ToDto(this Label label) => new(label.Id, label.Name, label.Color);

    public static Dto.BoardColumnDto ToDto(this BoardColumn column) =>
        new(column.Id, column.Name, column.Position, column.Category.ToDto());

    /// <summary>Resolves a user id, falling back to a placeholder for accounts that no longer exist.</summary>
    public static Dto.UserDto User(this IReadOnlyDictionary<Guid, Dto.UserDto> users, Guid id) =>
        users.TryGetValue(id, out var user) ? user : new Dto.UserDto(id, "", "Unknown user");

    private static TTo Convert<TFrom, TTo>(TFrom value)
        where TFrom : struct, Enum
        where TTo : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new DomainException($"'{value}' is not a valid {typeof(TFrom).Name}.");
        return (TTo)Enum.ToObject(typeof(TTo), System.Convert.ToInt32(value));
    }
}
