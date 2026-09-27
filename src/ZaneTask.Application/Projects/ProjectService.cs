using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;
using ZaneTask.Domain.Projects;
using DomainStatus = ZaneTask.Domain.Tasks.TaskItemStatus;

namespace ZaneTask.Application.Projects;

public sealed class ProjectService(
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
                p.Description,
                p.CreatedAt,
                MyRole = p.Members.First(m => m.UserId == me).Role,
                MemberCount = p.Members.Count(),
                OpenTaskCount = db.Tasks.Count(t => t.ProjectId == p.Id && t.Status != DomainStatus.Done),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ProjectSummaryDto(
                r.Id, r.Name, r.Description, r.MyRole.ToDto(), r.MemberCount, r.OpenTaskCount, r.CreatedAt))
            .ToList();
    }

    public async Task<ProjectDto> GetAsync(Guid projectId, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        return await ToDtoAsync(project, ct);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var project = Project.Create(request.Name, request.Description, Me, Now);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(project, ct);
    }

    public async Task<ProjectDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        project.EnsureOwner(Me);
        project.Update(request.Name, request.Description);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.Project);
        return await ToDtoAsync(project, ct);
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct);
        project.EnsureOwner(Me);
        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.ProjectDeleted);
    }

    public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddMemberRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct);
        project.EnsureOwner(Me);

        var user = await users.FindByEmailAsync(request.Email, ct)
            ?? throw new NotFoundException("User", request.Email);

        var member = project.AddMember(user.Id, request.Role.ToDomain(), Now);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.Project);
        return new ProjectMemberDto(user, member.Role.ToDto(), member.JoinedAt);
    }

    public async Task<ProjectMemberDto> ChangeMemberRoleAsync(
        Guid projectId, Guid userId, ChangeMemberRoleRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct);
        project.EnsureOwner(Me);
        project.ChangeMemberRole(userId, request.Role.ToDomain());
        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.Project);

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
            task.Assign(project, null, Now);

        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.Project);
    }

    public async Task<LabelDto> CreateLabelAsync(Guid projectId, SaveLabelRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        var label = project.AddLabel(request.Name, request.Color);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.Project);
        return label.ToDto();
    }

    public async Task<LabelDto> UpdateLabelAsync(Guid projectId, Guid labelId, SaveLabelRequest request, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        EnsureLabelExists(project, labelId);
        var label = project.UpdateLabel(labelId, request.Name, request.Color);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.Project);
        return label.ToDto();
    }

    public async Task DeleteLabelAsync(Guid projectId, Guid labelId, CancellationToken ct)
    {
        var project = await db.GetProjectForMemberAsync(projectId, Me, ct, includeLabels: true);
        EnsureLabelExists(project, labelId);
        project.RemoveLabel(labelId);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(projectId, BoardChange.Project);
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
        var myRole = project.Members.Single(m => m.UserId == Me).Role;

        return new ProjectDto(
            project.Id, project.Name, project.Description, myRole.ToDto(), project.CreatedAt, members, labels);
    }
}
