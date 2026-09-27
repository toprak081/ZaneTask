using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;
using ZaneTask.Domain.Common;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;
using DomainStatus = ZaneTask.Domain.Tasks.TaskItemStatus;

using ZaneTask.Application.Tasks;
using TaskActivityKind = ZaneTask.Domain.Tasks.TaskActivityKind;
namespace ZaneTask.Application.Projects;

public sealed class ProjectService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IUserDirectory users,
    TimeProvider clock)
{
    private Guid Me => currentUser.UserId;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ProjectSummaryDto>> ListAsync(CancellationToken ct)
    {
        var me = Me;
        var rows = await db.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == me))
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Key,
                p.Description,
                p.CreatedAt,
                MyRole = p.Members.First(m => m.UserId == me).Role,
                MemberCount = p.Members.Count(),
                OpenTaskCount = db.Tasks.Count(t => t.ProjectId == p.Id && t.Status != DomainStatus.Done),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ProjectSummaryDto(
                r.Id, r.Name, r.Key, r.Description, r.MyRole.ToDto(), r.MemberCount, r.OpenTaskCount, r.CreatedAt))
            .ToList();
    }

    public async Task<ProjectDto> GetAsync(Guid projectId, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        return await ToDtoAsync(project, ct);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(request.Key)
            ? await FirstFreeKeyAsync(ProjectKeys.Suggest(request.Name), ct)
            : await EnsureKeyIsFreeAsync(request.Key, exceptProjectId: null, ct);
        var project = Project.Create(request.Name, request.Description, key, Me, Now);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(project, ct);
    }

    public async Task<ProjectDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        project.EnsureOwner(Me);
        project.Update(request.Name, request.Description);
        if (!string.IsNullOrWhiteSpace(request.Key) && !string.Equals(request.Key, project.Key, StringComparison.OrdinalIgnoreCase))
            project.ChangeKey(await EnsureKeyIsFreeAsync(request.Key, projectId, ct));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(project, ct);
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct);
        project.EnsureOwner(Me);

        // Tasks reference board columns without cascading (so a column can never silently take tasks with it);
        // delete them explicitly so EF removes tasks before the columns.
        db.Tasks.RemoveRange(await db.Tasks.Where(t => t.ProjectId == projectId).ToListAsync(ct));
        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProjectDto> CreateColumnAsync(Guid projectId, SaveColumnRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        project.EnsureOwner(Me);
        project.AddColumn(request.Name, request.Category.ToDomain());
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(project, ct);
    }

    /// <summary>Renames a column and/or changes its category; the column's tasks follow the new category.</summary>
    public async Task<ProjectDto> UpdateColumnAsync(Guid projectId, Guid columnId, SaveColumnRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        project.EnsureOwner(Me);
        var column = GetColumnOrNotFound(project, columnId);

        if (!string.Equals(column.Name, request.Name.Trim(), StringComparison.Ordinal))
            project.RenameColumn(columnId, request.Name);

        var category = request.Category.ToDomain();
        if (column.Category != category)
        {
            project.SetColumnCategory(columnId, category);
            var tasks = await db.Tasks.Where(t => t.ColumnId == columnId).ToListAsync(ct);
            foreach (var task in tasks)
                task.SyncWithColumn(column);
        }

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(project, ct);
    }

    public async Task<ProjectDto> MoveColumnAsync(Guid projectId, Guid columnId, MoveColumnRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        project.EnsureOwner(Me);
        GetColumnOrNotFound(project, columnId);
        project.MoveColumn(columnId, request.Position);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(project, ct);
    }

    /// <summary>
    /// Deletes a column after moving its tasks to the bottom of <paramref name="moveTasksTo"/>
    /// (default: the leftmost other column). No task is ever lost.
    /// </summary>
    public async Task<ProjectDto> DeleteColumnAsync(Guid projectId, Guid columnId, Guid? moveTasksTo, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        project.EnsureOwner(Me);
        GetColumnOrNotFound(project, columnId);

        var target = moveTasksTo is { } targetId
            ? GetColumnOrNotFound(project, targetId)
            : project.OrderedColumns.FirstOrDefault(c => c.Id != columnId);
        if (target is null || target.Id == columnId)
            throw new DomainException("Choose another column to move this column's tasks to.");

        var projectTasks = await db.Tasks.Where(t => t.ProjectId == projectId).ToListAsync(ct);
        var removedName = project.GetColumn(columnId).Name;
        foreach (var task in projectTasks.Where(t => t.ColumnId == columnId))
            db.Record(task.Id, Me, TaskActivityKind.Moved, Now, removedName, target.Name);
        KanbanBoard.MoveAll(projectTasks, columnId, target, Now);
        project.RemoveColumn(columnId);

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(project, ct);
    }

    private static BoardColumn GetColumnOrNotFound(Project project, Guid columnId) =>
        project.Columns.FirstOrDefault(c => c.Id == columnId) ?? throw new NotFoundException("Column", columnId);

    public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddMemberRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct);
        project.EnsureOwner(Me);

        var user = await users.FindByEmailAsync(request.Email, ct)
            ?? throw new NotFoundException("User", request.Email);

        var member = project.AddMember(user.Id, request.Role.ToDomain(), Now);
        await db.SaveChangesAsync(ct);
        return new ProjectMemberDto(user, member.Role.ToDto(), member.JoinedAt);
    }

    public async Task<ProjectMemberDto> ChangeMemberRoleAsync(
        Guid projectId, Guid userId, ChangeMemberRoleRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct);
        project.EnsureOwner(Me);
        project.ChangeMemberRole(userId, request.Role.ToDomain());
        await db.SaveChangesAsync(ct);

        var member = project.Members.Single(m => m.UserId == userId);
        var directory = await users.GetByIdsAsync([userId], ct);
        return new ProjectMemberDto(directory.User(userId), member.Role.ToDto(), member.JoinedAt);
    }

    /// <summary>Owners can remove anyone; members can remove themselves (leave the project).</summary>
    public async Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct);
        if (userId != Me)
            project.EnsureOwner(Me);

        project.RemoveMember(userId);

        var assigned = await db.Tasks
            .Where(t => t.ProjectId == projectId && t.AssigneeId == userId)
            .ToListAsync(ct);
        foreach (var task in assigned)
        {
            task.Assign(project, null, Now);
            db.Record(task.Id, Me, TaskActivityKind.AssigneeChanged, Now, userId.ToString(), null);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<LabelDto> CreateLabelAsync(Guid projectId, SaveLabelRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        var label = project.AddLabel(request.Name, request.Color);
        await db.SaveChangesAsync(ct);
        return label.ToDto();
    }

    public async Task<LabelDto> UpdateLabelAsync(Guid projectId, Guid labelId, SaveLabelRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        EnsureLabelExists(project, labelId);
        var label = project.UpdateLabel(labelId, request.Name, request.Color);
        await db.SaveChangesAsync(ct);
        return label.ToDto();
    }

    public async Task DeleteLabelAsync(Guid projectId, Guid labelId, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        EnsureLabelExists(project, labelId);
        project.RemoveLabel(labelId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Validates <paramref name="key"/> and makes sure no other project uses it.</summary>
    private async Task<string> EnsureKeyIsFreeAsync(string key, Guid? exceptProjectId, CancellationToken ct)
    {
        var normalized = Project.NormalizeKey(key);
        if (await db.Projects.AnyAsync(p => p.Key == normalized && p.Id != exceptProjectId, ct))
            throw new ConflictException($"The key {normalized} is already used by another project.");
        return normalized;
    }

    /// <summary>The suggested key, or the same key with a number appended (WEB, WEB2, WEB3…).</summary>
    private async Task<string> FirstFreeKeyAsync(string suggestion, CancellationToken ct)
    {
        var stem = suggestion.Length > Project.KeyMaxLength - 2 ? suggestion[..(Project.KeyMaxLength - 2)] : suggestion;
        var taken = await db.Projects
            .Where(p => p.Key.StartsWith(stem))
            .Select(p => p.Key)
            .ToListAsync(ct);
        if (!taken.Contains(suggestion))
            return suggestion;
        for (var n = 2; ; n++)
        {
            var candidate = $"{stem}{n}";
            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    private static void EnsureLabelExists(Project project, Guid labelId)
    {
        if (project.Labels.All(l => l.Id != labelId))
            throw new NotFoundException("Label", labelId);
    }

    private async Task<ProjectDto> ToDtoAsync(Project project, CancellationToken ct)
    {
        var directory = await users.GetByIdsAsync(project.Members.Select(m => m.UserId), ct);
        var members = project.Members
            .OrderByDescending(m => m.Role)
            .ThenBy(m => directory.User(m.UserId).DisplayName)
            .Select(m => new ProjectMemberDto(directory.User(m.UserId), m.Role.ToDto(), m.JoinedAt))
            .ToList();
        var labels = project.Labels.OrderBy(l => l.Name).Select(l => l.ToDto()).ToList();
        var columns = project.OrderedColumns.Select(c => c.ToDto()).ToList();
        var myRole = project.Members.Single(m => m.UserId == Me).Role;

        return new ProjectDto(
            project.Id, project.Name, project.Key, project.Description, myRole.ToDto(), project.CreatedAt, members, labels, columns);
    }
}
