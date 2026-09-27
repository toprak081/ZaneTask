using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZaneTask.Application.Abstractions;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;
using ZaneTask.Infrastructure.Identity;

namespace ZaneTask.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.IsSqlite())
            MarkDateTimesAsUtc(builder);
    }

    /// <summary>
    /// All timestamps are written in UTC, but SQLite stores them as text without a zone and reads them back as
    /// <see cref="DateTimeKind.Unspecified"/>. Restore the kind so they serialize and convert to local time correctly.
    /// </summary>
    private static void MarkDateTimesAsUtc(ModelBuilder builder)
    {
        var utc = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableUtc = new ValueConverter<DateTime?, DateTime?>(
            v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var property in builder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(utc);
            else if (property.ClrType == typeof(DateTime?))
                property.SetValueConverter(nullableUtc);
        }
    }
}
