using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Application.Tasks;

/// <summary>A task's checklist. Every change returns the whole, re-ordered list so clients can simply replace theirs.</summary>
public sealed class ChecklistService(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    private Guid Me => currentUser.UserId;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ChecklistItemDto>> ListAsync(Guid taskId, CancellationToken ct)
    {
        var task = await LoadTaskAsync(t => t.Id == taskId, () => new NotFoundException("Task", taskId), ct);
        return ToDtos(task);
    }

    public async Task<IReadOnlyList<ChecklistItemDto>> AddAsync(Guid taskId, AddChecklistItemRequest request, CancellationToken ct)
    {
        var task = await LoadTaskAsync(t => t.Id == taskId, () => new NotFoundException("Task", taskId), ct);
        task.AddChecklistItem(request.Text, Now);
        await db.SaveChangesAsync(ct);
        return ToDtos(task);
    }

    public async Task<IReadOnlyList<ChecklistItemDto>> UpdateAsync(Guid itemId, UpdateChecklistItemRequest request, CancellationToken ct)
    {
        var task = await LoadTaskOfItemAsync(itemId, ct);
        if (request.Text is not null)
            task.RenameChecklistItem(itemId, request.Text, Now);
        if (request.IsDone is { } done)
            task.SetChecklistItemDone(itemId, done, Now);
        await db.SaveChangesAsync(ct);
        return ToDtos(task);
    }

    public async Task<IReadOnlyList<ChecklistItemDto>> MoveAsync(Guid itemId, MoveChecklistItemRequest request, CancellationToken ct)
    {
        var task = await LoadTaskOfItemAsync(itemId, ct);
        task.MoveChecklistItem(itemId, request.Position, Now);
        await db.SaveChangesAsync(ct);
        return ToDtos(task);
    }

    public async Task<IReadOnlyList<ChecklistItemDto>> DeleteAsync(Guid itemId, CancellationToken ct)
    {
        var task = await LoadTaskOfItemAsync(itemId, ct);
        task.RemoveChecklistItem(itemId, Now);
        await db.SaveChangesAsync(ct);
        return ToDtos(task);
    }

    private Task<TaskItem> LoadTaskOfItemAsync(Guid itemId, CancellationToken ct) =>
        LoadTaskAsync(t => t.Checklist.Any(c => c.Id == itemId), () => new NotFoundException("Checklist item", itemId), ct);

    /// <summary>Loads a task with its checklist; anything outside the user's projects is reported as not found.</summary>
    private async Task<TaskItem> LoadTaskAsync(
        System.Linq.Expressions.Expression<Func<TaskItem, bool>> predicate,
        Func<Exception> notFound,
        CancellationToken ct)
    {
        var me = Me;
        var task = await db.Tasks.Include(t => t.Checklist).FirstOrDefaultAsync(predicate, ct);
        if (task is null ||
            !await db.Projects.AnyAsync(p => p.Id == task.ProjectId && p.Members.Any(m => m.UserId == me), ct))
        {
            throw notFound();
        }
        return task;
    }

    private static IReadOnlyList<ChecklistItemDto> ToDtos(TaskItem task) =>
        task.Checklist
            .OrderBy(i => i.Position)
            .Select(i => new ChecklistItemDto(i.Id, i.Text, i.IsDone, i.Position))
            .ToList();
}
