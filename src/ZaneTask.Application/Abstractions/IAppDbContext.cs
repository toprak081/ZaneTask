using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<Comment> Comments { get; }
    DbSet<Label> Labels { get; }
    DbSet<TaskActivity> TaskActivities { get; }

    /// <summary>Used to discard pending changes before retrying an operation.</summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
