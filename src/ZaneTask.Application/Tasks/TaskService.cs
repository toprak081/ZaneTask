using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;
using ZaneTask.Domain.Tasks;
using TaskItemStatus = ZaneTask.Contracts.TaskItemStatus;
using TaskType = ZaneTask.Contracts.TaskType;

using TaskActivityKind = ZaneTask.Domain.Tasks.TaskActivityKind;
namespace ZaneTask.Application.Tasks;

public sealed record TaskFilter(
    TaskItemStatus? Status = null,
    Guid? AssigneeId = null,
    Guid? LabelId = null,
    string? Search = null,
    TaskType? Type = null,
    Guid? ColumnId = null);

public sealed partial class TaskService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IUserDirectory users,
    TimeProvider clock)
{
    private Guid Me => currentUser.UserId;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Tasks of a project ordered for a kanban board: by status, then position.</summary>
    public async Task<IReadOnlyList<TaskDto>> ListAsync(Guid projectId, TaskFilter filter, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct);

        var query = db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId);
        if (filter.ColumnId is { } columnId)
            query = query.Where(t => t.ColumnId == columnId);
        if (filter.Status is { } status)
        {
            var domainStatus = status.ToDomain();
            query = query.Where(t => t.Status == domainStatus);
        }
        if (filter.AssigneeId is { } assigneeId)
            query = query.Where(t => t.AssigneeId == assigneeId);
        if (filter.LabelId is { } labelId)
            query = query.Where(t => t.Labels.Any(l => l.Id == labelId));
        if (filter.Type is { } type)
        {
            var domainType = type.ToDomain();
            query = query.Where(t => t.Type == domainType);
        }
        if (filter.Search is { } search && TaskKeyRegex().Match(search.Trim()) is { Success: true } key &&
            string.Equals(key.Groups["project"].Value, project.Key, StringComparison.OrdinalIgnoreCase))
        {
            // "WEB-12" finds that task directly.
            var number = int.Parse(key.Groups["number"].Value);
            query = query.Where(t => t.Number == number);
        }
        else if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim().ToLower()}%";
            query = query.Where(t =>
                EF.Functions.Like(t.Title.ToLower(), pattern) ||
                (t.Description != null && EF.Functions.Like(t.Description.ToLower(), pattern)));
        }

        var rows = await query
            .Select(t => new TaskRow(t, t.Labels.ToList(), t.Comments.Count(), t.Checklist.Count(c => c.IsDone), t.Checklist.Count()))
            .ToListAsync(ct);

        // Board order: column position, then position inside the column.
        var columnOrder = project.Columns.ToDictionary(c => c.Id, c => c.Position);
        var ordered = rows
            .OrderBy(r => columnOrder.GetValueOrDefault(r.Task.ColumnId, int.MaxValue))
            .ThenBy(r => r.Task.Position)
            .ToList();
        return await ToDtosAsync(ordered, ct);
    }

    /// <summary>
    /// Tasks assigned to the current user across all their projects: soonest due date first
    /// (undated last), then highest priority.
    /// </summary>
    public async Task<IReadOnlyList<MyTaskDto>> ListMineAsync(bool includeDone, CancellationToken ct)
    {
        var me = Me;
        var query = db.Tasks.AsNoTracking()
            .Where(t => t.AssigneeId == me)
            .Where(t => db.Projects.Any(p => p.Id == t.ProjectId && p.Members.Any(m => m.UserId == me)));
        if (!includeDone)
            query = query.Where(t => t.Status != Domain.Tasks.TaskItemStatus.Done);

        var rows = await query
            .Select(t => new TaskRow(t, t.Labels.ToList(), t.Comments.Count(), t.Checklist.Count(c => c.IsDone), t.Checklist.Count()))
            .ToListAsync(ct);

        var projectIds = rows.Select(r => r.Task.ProjectId).Distinct().ToList();
        var projectNames = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var ordered = rows
            .OrderBy(r => r.Task.DueDate is null)
            .ThenBy(r => r.Task.DueDate)
            .ThenByDescending(r => r.Task.Priority)
            .ThenBy(r => r.Task.CreatedAt)
            .ToList();

        var dtos = await ToDtosAsync(ordered, ct);
        return dtos.Select(t => new MyTaskDto(t, projectNames[t.ProjectId])).ToList();
    }

    public async Task<TaskDto> GetAsync(Guid taskId, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> CreateAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
            var column = request.ColumnId is { } columnId
                ? project.GetColumn(columnId)
                : project.DefaultColumn(request.Status.ToDomain());
            var position = await db.Tasks.CountAsync(t => t.ColumnId == column.Id, ct);

            var task = TaskItem.Create(
                project,
                request.Title,
                request.Description,
                request.Type.ToDomain(),
                column,
                request.Priority.ToDomain(),
                request.DueDate,
                request.AssigneeId,
                Me,
                position,
                Now);

            foreach (var labelId in request.LabelIds?.Distinct() ?? [])
                task.AddLabel(project.GetLabel(labelId));

            db.Tasks.Add(task);
            db.Record(task.Id, Me, TaskActivityKind.Created, Now);
            try
            {
                await db.SaveChangesAsync(ct);
                return await ToDtoAsync(task, ct);
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                // Someone else created a task in this project at the same moment and took this number
                // (unique index on ProjectId + Number). Reload the project's counter and try again.
                db.ChangeTracker.Clear();
            }
        }
    }

    public async Task<TaskDto> UpdateAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var before = (task.Title, task.Description, task.Type, task.Priority, task.DueDate);
        task.Update(request.Title, request.Description, request.Type.ToDomain(), request.Priority.ToDomain(), request.DueDate, Now);

        db.RecordChange(task.Id, Me, TaskActivityKind.TitleChanged, Now, before.Title, task.Title, v => v);
        if (before.Description != task.Description)
            db.Record(task.Id, Me, TaskActivityKind.DescriptionChanged, Now);
        db.RecordChange(task.Id, Me, TaskActivityKind.TypeChanged, Now, before.Type, task.Type, v => v.ToString());
        db.RecordChange(task.Id, Me, TaskActivityKind.PriorityChanged, Now, before.Priority, task.Priority, v => v.ToString());
        db.RecordChange(task.Id, Me, TaskActivityKind.DueDateChanged, Now, before.DueDate, task.DueDate, TaskActivityLog.Iso);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> AssignAsync(Guid taskId, AssignTaskRequest request, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var project = await db.GetProjectForMemberAsync(task.ProjectId, Me, ct);
        var before = task.AssigneeId;
        task.Assign(project, request.AssigneeId, Now);
        db.RecordChange(task.Id, Me, TaskActivityKind.AssigneeChanged, Now, before, task.AssigneeId, v => v?.ToString());
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> MoveAsync(Guid taskId, MoveTaskRequest request, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var project = await db.GetProjectForMemberAsync(task.ProjectId, Me, ct);
        var column = (request.ColumnId, request.Category) switch
        {
            ({ } columnId, _) => project.GetColumn(columnId),
            (null, { } category) => project.DefaultColumn(category.ToDomain()),
            _ => throw new Domain.Common.DomainException("Choose a column to move the task to."),
        };
        var projectTasks = await db.Tasks.Where(t => t.ProjectId == task.ProjectId).ToListAsync(ct);

        var from = project.Columns.FirstOrDefault(c => c.Id == task.ColumnId);
        KanbanBoard.Move(projectTasks, task, column, request.Position, Now);
        if (from?.Id != column.Id)
            db.Record(task.Id, Me, TaskActivityKind.Moved, Now, from?.Name, column.Name); // reordering within a column isn't history
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    /// <summary>The task's creator and project owners can delete a task.</summary>
    public async Task DeleteAsync(Guid taskId, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        if (task.CreatedById != Me)
        {
            var project = await db.GetProjectForMemberAsync(task.ProjectId, Me, ct);
            project.EnsureOwner(Me);
        }

        var projectTasks = await db.Tasks.Where(t => t.ProjectId == task.ProjectId).ToListAsync(ct);
        KanbanBoard.Remove(projectTasks, task);
        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TaskDto> AddLabelAsync(Guid taskId, Guid labelId, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var label = await db.Labels.FirstOrDefaultAsync(l => l.Id == labelId && l.ProjectId == task.ProjectId, ct)
            ?? throw new NotFoundException("Label", labelId);
        if (task.Labels.All(l => l.Id != labelId))
        {
            task.AddLabel(label);
            db.Record(task.Id, Me, TaskActivityKind.LabelAdded, Now, newValue: label.Name);
        }
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> RemoveLabelAsync(Guid taskId, Guid labelId, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        if (task.Labels.FirstOrDefault(l => l.Id == labelId) is { } label)
        {
            task.RemoveLabel(labelId);
            db.Record(task.Id, Me, TaskActivityKind.LabelRemoved, Now, oldValue: label.Name);
        }
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    /// <summary>The task's history, newest first (at most 200 entries).</summary>
    public async Task<IReadOnlyList<TaskActivityDto>> ListActivityAsync(Guid taskId, CancellationToken ct)
    {
        await LoadTaskForMemberAsync(taskId, ct);
        var entries = await db.TaskActivities.AsNoTracking()
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.At).ThenByDescending(a => a.Id)
            .Take(200)
            .ToListAsync(ct);

        // Assignee changes store user ids; show names instead.
        var userIds = entries.Select(a => a.ActorId)
            .Concat(entries.Where(a => a.Kind == TaskActivityKind.AssigneeChanged)
                .SelectMany(a => new[] { a.OldValue, a.NewValue })
                .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty))
            .Distinct();
        var directory = await users.GetByIdsAsync(userIds, ct);
        string? Value(TaskActivity a, string? value) =>
            a.Kind == TaskActivityKind.AssigneeChanged && Guid.TryParse(value, out var id) ? directory.User(id).DisplayName : value;

        return entries
            .Select(a => new TaskActivityDto(a.Id, directory.User(a.ActorId), a.Kind.ToDto(), Value(a, a.OldValue), Value(a, a.NewValue), a.At))
            .ToList();
    }

    private async Task<TaskItem> LoadTaskForMemberAsync(Guid taskId, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.Labels).FirstOrDefaultAsync(t => t.Id == taskId, ct);
        var me = Me;
        if (task is null ||
            !await db.Projects.AnyAsync(p => p.Id == task.ProjectId && p.Members.Any(m => m.UserId == me), ct))
        {
            throw new NotFoundException("Task", taskId);
        }
        return task;
    }

    private async Task<TaskDto> ToDtoAsync(TaskItem task, CancellationToken ct)
    {
        var commentCount = await db.Comments.CountAsync(c => c.TaskId == task.Id, ct);
        var checklist = await db.Tasks.Where(t => t.Id == task.Id)
            .Select(t => new { Done = t.Checklist.Count(c => c.IsDone), Total = t.Checklist.Count() })
            .SingleAsync(ct);
        var dtos = await ToDtosAsync([new TaskRow(task, task.Labels.ToList(), commentCount, checklist.Done, checklist.Total)], ct);
        return dtos[0];
    }

    private async Task<IReadOnlyList<TaskDto>> ToDtosAsync(IReadOnlyList<TaskRow> rows, CancellationToken ct)
    {
        var userIds = rows
            .SelectMany(r => new[] { r.Task.CreatedById, r.Task.AssigneeId ?? Guid.Empty })
            .Where(id => id != Guid.Empty)
            .Distinct();
        var directory = await users.GetByIdsAsync(userIds, ct);

        var projectIds = rows.Select(r => r.Task.ProjectId).Distinct().ToList();
        var projectKeys = await db.Projects.AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Key, ct);

        return rows.Select(r => new TaskDto(
                r.Task.Id,
                r.Task.ProjectId,
                r.Task.Number,
                $"{projectKeys[r.Task.ProjectId]}-{r.Task.Number}",
                r.Task.Type.ToDto(),
                r.Task.Title,
                r.Task.Description,
                r.Task.Status.ToDto(),
                r.Task.ColumnId,
                r.Task.Priority.ToDto(),
                r.Task.DueDate,
                r.Task.AssigneeId is { } assigneeId ? directory.User(assigneeId) : null,
                r.Task.Position,
                directory.User(r.Task.CreatedById),
                r.Task.CreatedAt,
                r.Task.UpdatedAt,
                r.Labels.OrderBy(l => l.Name).Select(l => l.ToDto()).ToList(),
                r.CommentCount,
                r.ChecklistDone,
                r.ChecklistTotal))
            .ToList();
    }

    private sealed record TaskRow(
        TaskItem Task, List<Domain.Projects.Label> Labels, int CommentCount, int ChecklistDone, int ChecklistTotal);

    [GeneratedRegex(@"^(?<project>[A-Za-z][A-Za-z0-9]*)-(?<number>\d{1,9})$")]
    private static partial Regex TaskKeyRegex();
}
