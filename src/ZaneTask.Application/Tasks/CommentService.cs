using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Application.Tasks;

public sealed class CommentService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IUserDirectory users,
    TimeProvider clock)
{
    private Guid Me => currentUser.UserId;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<CommentDto>> ListAsync(Guid taskId, CancellationToken ct)
    {
        await EnsureTaskVisibleAsync(taskId, ct);

        var comments = await db.Comments
            .AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var directory = await users.GetByIdsAsync(comments.Select(c => c.AuthorId).Distinct(), ct);
        return comments.Select(c => ToDto(c, directory)).ToList();
    }

    public async Task<CommentDto> AddAsync(Guid taskId, SaveCommentRequest request, CancellationToken ct)
    {
        var task = await EnsureTaskVisibleAsync(taskId, ct);
        var comment = task.AddComment(Me, request.Body, Now);
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(comment, ct);
    }

    public async Task<CommentDto> UpdateAsync(Guid commentId, SaveCommentRequest request, CancellationToken ct)
    {
        var (comment, _) = await LoadCommentAsync(commentId, ct);
        if (comment.AuthorId != Me)
            throw new ForbiddenException("Only the author can edit a comment.");

        comment.Edit(Me, request.Body, Now);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(comment, ct);
    }

    /// <summary>The author and project owners can delete a comment.</summary>
    public async Task DeleteAsync(Guid commentId, CancellationToken ct)
    {
        var (comment, projectId) = await LoadCommentAsync(commentId, ct);
        if (comment.AuthorId != Me)
        {
            var project = await db.GetProjectForMemberAsync(projectId, Me, ct);
            project.EnsureOwner(Me);
        }

        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);
    }

    private async Task<TaskItem> EnsureTaskVisibleAsync(Guid taskId, CancellationToken ct)
    {
        var me = Me;
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null ||
            !await db.Projects.AnyAsync(p => p.Id == task.ProjectId && p.Members.Any(m => m.UserId == me), ct))
        {
            throw new NotFoundException("Task", taskId);
        }
        return task;
    }

    private async Task<(Comment Comment, Guid ProjectId)> LoadCommentAsync(Guid commentId, CancellationToken ct)
    {
        var me = Me;
        var row = await db.Comments
            .Where(c => c.Id == commentId)
            .Select(c => new
            {
                Comment = c,
                ProjectId = db.Tasks.Where(t => t.Id == c.TaskId).Select(t => t.ProjectId).First(),
            })
            .FirstOrDefaultAsync(ct);

        if (row is null ||
            !await db.Projects.AnyAsync(p => p.Id == row.ProjectId && p.Members.Any(m => m.UserId == me), ct))
        {
            throw new NotFoundException("Comment", commentId);
        }
        return (row.Comment, row.ProjectId);
    }

    private async Task<CommentDto> ToDtoAsync(Comment comment, CancellationToken ct)
    {
        var directory = await users.GetByIdsAsync([comment.AuthorId], ct);
        return ToDto(comment, directory);
    }

    private static CommentDto ToDto(Comment c, IReadOnlyDictionary<Guid, UserDto> directory) =>
        new(c.Id, c.TaskId, directory.User(c.AuthorId), c.Body, c.CreatedAt, c.EditedAt);
}
