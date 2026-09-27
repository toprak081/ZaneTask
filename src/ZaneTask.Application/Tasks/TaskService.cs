using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;
using ZaneTask.Domain.Tasks;
using TaskItemStatus = ZaneTask.Contracts.TaskItemStatus;
using TaskType = ZaneTask.Contracts.TaskType;

namespace ZaneTask.Application.Tasks;

public sealed record TaskFilter(
    TaskItemStatus? Status = null,
    Guid? AssigneeId = null,
    Guid? LabelId = null,
    string? Search = null,
    TaskType? Type = null);

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
            .Select(t => new TaskRow(t, t.Labels.ToList(), t.Comments.Count()))
            .ToListAsync(ct);

        // Status is stored as text, so order by the enum value in memory rather than alphabetically in SQL.
        var ordered = rows.OrderBy(r => r.Task.Status).ThenBy(r => r.Task.Position).ToList();
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
            .Select(t => new TaskRow(t, t.Labels.ToList(), t.Comments.Count()))
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
            var status = request.Status.ToDomain();
            var position = await db.Tasks.CountAsync(t => t.ProjectId == projectId && t.Status == status, ct);

            var task = TaskItem.Create(
                project,
                request.Title,
                request.Description,
                request.Type.ToDomain(),
                status,
                request.Priority.ToDomain(),
                request.DueDate,
                request.AssigneeId,
                Me,
                position,
                Now);

            foreach (var labelId in request.LabelIds?.Distinct() ?? [])
                task.AddLabel(project.GetLabel(labelId));

            db.Tasks.Add(task);
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
        task.Update(request.Title, request.Description, request.Type.ToDomain(), request.Priority.ToDomain(), request.DueDate, Now);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> AssignAsync(Guid taskId, AssignTaskRequest request, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var project = await db.GetProjectForMemberAsync(task.ProjectId, Me, ct);
        task.Assign(project, request.AssigneeId, Now);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> MoveAsync(Guid taskId, MoveTaskRequest request, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var projectTasks = await db.Tasks.Where(t => t.ProjectId == task.ProjectId).ToListAsync(ct);

        KanbanBoard.Move(projectTasks, task, request.Status.ToDomain(), request.Position, Now);
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
        task.AddLabel(label);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> RemoveLabelAsync(Guid taskId, Guid labelId, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        task.RemoveLabel(labelId);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(task, ct);
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
        var dtos = await ToDtosAsync([new TaskRow(task, task.Labels.ToList(), commentCount)], ct);
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
                r.Task.Priority.ToDto(),
                r.Task.DueDate,
                r.Task.AssigneeId is { } assigneeId ? directory.User(assigneeId) : null,
                r.Task.Position,
                directory.User(r.Task.CreatedById),
                r.Task.CreatedAt,
                r.Task.UpdatedAt,
                r.Labels.OrderBy(l => l.Name).Select(l => l.ToDto()).ToList(),
                r.CommentCount))
            .ToList();
    }

    private sealed record TaskRow(TaskItem Task, List<Domain.Projects.Label> Labels, int CommentCount);

    [GeneratedRegex(@"^(?<project>[A-Za-z][A-Za-z0-9]*)-(?<number>\d{1,9})$")]
    private static partial Regex TaskKeyRegex();
}
