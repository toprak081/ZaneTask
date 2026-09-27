using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Domain.Projects;

namespace ZaneTask.Application.Common;

internal static class ProjectAccess
{
    /// <summary>
    /// Loads a project with its members (and optionally labels). Non-members get a 404 rather than a 403
    /// so that project ids cannot be probed.
    /// </summary>
    public static async Task<Project> GetProjectForMemberAsync(
        this IAppDbContext db,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken,
        bool includeLabels = false)
    {
        var query = db.Projects.Include(p => p.Members).AsQueryable();
        if (includeLabels)
            query = query.Include(p => p.Labels);

        var project = await query.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null || !project.IsMember(userId))
            throw new NotFoundException("Project", projectId);
        return project;
    }

    public static void EnsureOwner(this Project project, Guid userId)
    {
        if (!project.IsOwner(userId))
            throw new ForbiddenException("Only project owners can do this.");
    }
}
