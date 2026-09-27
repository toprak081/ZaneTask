using Microsoft.EntityFrameworkCore;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;

namespace ZaneTask.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<Comment> Comments { get; }
    DbSet<Label> Labels { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
