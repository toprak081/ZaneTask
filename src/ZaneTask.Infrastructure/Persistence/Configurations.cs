using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZaneTask.Domain.Projects;
using ZaneTask.Domain.Tasks;
using ZaneTask.Infrastructure.Identity;

namespace ZaneTask.Infrastructure.Persistence;

// Entity ids are generated client-side (Guid v7), so they are marked ValueGeneratedNever:
// that way EF treats entities reached through navigations as new instead of as existing rows.

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(ApplicationUser.DisplayNameMaxLength).IsRequired();
    }
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
        builder.Property(p => p.Key).HasMaxLength(Project.KeyMaxLength).IsRequired();
        builder.HasIndex(p => p.Key).IsUnique();
        builder.Property(p => p.Description).HasMaxLength(Project.DescriptionMaxLength);

        builder.HasMany(p => p.Members).WithOne().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Members).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Labels).WithOne().HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");
        builder.HasKey(m => new { m.ProjectId, m.UserId });
        builder.HasIndex(m => m.UserId);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("Labels");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).HasMaxLength(Label.NameMaxLength).IsRequired();
        builder.Property(l => l.Color).HasMaxLength(7).IsFixedLength().IsRequired();
        builder.HasIndex(l => new { l.ProjectId, l.Name }).IsUnique();
    }
}

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).HasMaxLength(TaskItem.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(TaskItem.DescriptionMaxLength);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(t => new { t.ProjectId, t.Number }).IsUnique();
        builder.HasIndex(t => new { t.ProjectId, t.Status, t.Position });
        builder.HasIndex(t => t.AssigneeId);

        builder.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Comments).WithOne().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Checklist).WithOne().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Checklist).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(t => t.Labels).WithMany().UsingEntity(
            "TaskLabels",
            r => r.HasOne(typeof(Label)).WithMany().HasForeignKey("LabelId").OnDelete(DeleteBehavior.Cascade),
            l => l.HasOne(typeof(TaskItem)).WithMany().HasForeignKey("TaskId").OnDelete(DeleteBehavior.Cascade));
        builder.Navigation(t => t.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("ChecklistItems");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Text).HasMaxLength(ChecklistItem.TextMaxLength).IsRequired();
        builder.HasIndex(c => new { c.TaskId, c.Position });
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Body).HasMaxLength(Comment.BodyMaxLength).IsRequired();
        builder.HasIndex(c => new { c.TaskId, c.CreatedAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}
