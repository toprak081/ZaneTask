using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;
using ZaneTask.Domain.Tasks;
using TaskItemStatus = ZaneTask.Contracts.TaskItemStatus;

namespace ZaneTask.Application.Tasks;

public sealed record TaskFilter(
    TaskItemStatus? Status = null,
    Guid? AssigneeId = null,
    Guid? LabelId = null,
    string? Search = null);

public sealed class TaskService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IUserDirectory users,
    IBoardNotifier notifier,
    TimeProvider clock)
{
    private Guid Me => currentUser.UserId;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // Sent after a successful save; never with the request's token, so a cancelled request can't skip it.
    private Task NotifyAsync(Guid projectId, BoardChange change, Guid? taskId = null) =>
        notifier.NotifyAsync(new BoardEvent(projectId, change, taskId, Me), CancellationToken.None);

    /// <summary>Tasks of a project ordered for a kanban board: by status, then position.</summary>
    public async Task<IReadOnlyList<TaskDto>> ListAsync(Guid projectId, TaskFilter filter, CancellationToken ct)
    {
        await db.GetProjectForMemberAsync(projectId, Me, ct);

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
        if (!string.IsNullOrWhiteSpace(filter.Search))
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

    public async Task<TaskDto> GetAsync(Guid taskId, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> CreateAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        var status = request.Status.ToDomain();
        var position = await db.Tasks.CountAsync(t => t.ProjectId == projectId && t.Status == status, ct);

        var task = TaskItem.Create(
            project,
            request.Title,
            request.Description,
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
        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.Tasks, task.Id);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> UpdateAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        task.Update(request.Title, request.Description, request.Priority.ToDomain(), request.DueDate, Now);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(task.ProjectId, BoardChange.Tasks, task.Id);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> AssignAsync(Guid taskId, AssignTaskRequest request, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var project = await db.GetProjectForMemberAsync(task.ProjectId, Me, ct);
        task.Assign(project, request.AssigneeId, Now);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(task.ProjectId, BoardChange.Tasks, task.Id);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> MoveAsync(Guid taskId, MoveTaskRequest request, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var projectTasks = await db.Tasks.Where(t => t.ProjectId == task.ProjectId).ToListAsync(ct);

        KanbanBoard.Move(projectTasks, task, request.Status.ToDomain(), request.Position, Now);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(task.ProjectId, BoardChange.Tasks, task.Id);
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
        await NotifyAsync(task.ProjectId, BoardChange.Tasks, task.Id);
    }

    public async Task<TaskDto> AddLabelAsync(Guid taskId, Guid labelId, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        var label = await db.Labels.FirstOrDefaultAsync(l => l.Id == labelId && l.ProjectId == task.ProjectId, ct)
            ?? throw new NotFoundException("Label", labelId);
        task.AddLabel(label);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(task.ProjectId, BoardChange.Tasks, task.Id);
        return await ToDtoAsync(task, ct);
    }

    public async Task<TaskDto> RemoveLabelAsync(Guid taskId, Guid labelId, CancellationToken ct)
    {
        var task = await LoadTaskForMemberAsync(taskId, ct);
        task.RemoveLabel(labelId);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(task.ProjectId, BoardChange.Tasks, task.Id);
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

        return rows.Select(r => new TaskDto(
                r.Task.Id,
                r.Task.ProjectId,
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
}
