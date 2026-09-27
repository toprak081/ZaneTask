using ZaneTask.Application.Abstractions;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Application.Tasks;

/// <summary>Adds history entries to the unit of work, so they are saved together with the change they describe.</summary>
internal static class TaskActivityLog
{
    public static void Record(
        this IAppDbContext db, Guid taskId, Guid actorId, TaskActivityKind kind, DateTime now,
        string? oldValue = null, string? newValue = null) =>
        db.TaskActivities.Add(TaskActivity.Create(taskId, actorId, kind, oldValue, newValue, now));

    /// <summary>Records a change only when the value actually changed.</summary>
    public static void RecordChange<T>(
        this IAppDbContext db, Guid taskId, Guid actorId, TaskActivityKind kind, DateTime now,
        T before, T after, Func<T, string?> format)
    {
        if (!EqualityComparer<T>.Default.Equals(before, after))
            db.Record(taskId, actorId, kind, now, format(before), format(after));
    }

    /// <summary>Dates are stored as ISO text (yyyy-MM-dd) and formatted by the client.</summary>
    public static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd");
}
