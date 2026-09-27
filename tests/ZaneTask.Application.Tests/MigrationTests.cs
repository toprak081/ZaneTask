using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZaneTask.Infrastructure.Persistence;

namespace ZaneTask.Application.Tests;

/// <summary>Guards against forgetting to add a migration after changing the model, for both databases.</summary>
public sealed class MigrationTests
{
    [Fact]
    public void Postgres_migrations_match_the_model()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .Options);

        Assert.False(db.Database.HasPendingModelChanges(),
            "Run: dotnet ef migrations add <Name> -p src/ZaneTask.Infrastructure -s src/ZaneTask.Api -o Persistence/Migrations");
    }

    [Fact]
    public void Sqlite_migrations_match_the_model_and_apply_cleanly()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection, sqlite => sqlite.MigrationsAssembly(ZaneTask.Infrastructure.DependencyInjection.SqliteMigrationsAssembly))
            .Options);

        Assert.False(db.Database.HasPendingModelChanges(),
            "Run (with Database__Provider=Sqlite): dotnet ef migrations add <Name> -p src/ZaneTask.Infrastructure.Sqlite -s src/ZaneTask.Api -o Migrations");

        db.Database.Migrate();
        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.Equal(0, db.Projects.Count());
    }

    [Fact]
    public void Upgrading_existing_data_assigns_project_keys_and_task_numbers()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = SqliteContext(connection);
        db.GetService<IMigrator>().Migrate("InitialCreate");

        // Data as the first release stored it: no keys, no numbers.
        var user = NewId();
        var (older, newer) = (NewId(), NewId());
        Exec(connection, """
            INSERT INTO "AspNetUsers" ("Id", "DisplayName", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            VALUES ($user, 'Old User', 0, 0, 0, 0, 0);
            INSERT INTO "Projects" ("Id", "Name", "CreatedAt") VALUES ($newer, 'Newer', '2026-02-01 00:00:00');
            INSERT INTO "Projects" ("Id", "Name", "CreatedAt") VALUES ($older, 'Older', '2026-01-01 00:00:00');
            """, ("$user", user), ("$older", older), ("$newer", newer));
        foreach (var (project, title, created) in new[]
                 {
                     (older, "second", "2026-01-03 00:00:00"),
                     (older, "first", "2026-01-02 00:00:00"),
                     (newer, "only", "2026-02-02 00:00:00"),
                 })
        {
            Exec(connection, """
                INSERT INTO "Tasks" ("Id", "ProjectId", "Title", "Status", "Priority", "Position", "CreatedById", "CreatedAt", "UpdatedAt")
                VALUES ($id, $project, $title, 'Todo', 'Medium', 0, $user, $created, $created);
                """, ("$id", NewId()), ("$project", project), ("$title", title), ("$user", user), ("$created", created));
        }

        db.Database.Migrate();

        var projects = db.Projects.OrderBy(p => p.Key).Select(p => new { p.Name, p.Key, p.NextTaskNumber }).ToList();
        Assert.Equal(
            [new { Name = "Older", Key = "P1", NextTaskNumber = 3 }, new { Name = "Newer", Key = "P2", NextTaskNumber = 2 }],
            projects);

        var tasks = db.Tasks.OrderBy(t => t.Title).Select(t => new { t.Title, t.Number, t.Type }).ToList();
        Assert.Equal(
            [
                new { Title = "first", Number = 1, Type = Domain.Tasks.TaskType.Task },
                new { Title = "only", Number = 1, Type = Domain.Tasks.TaskType.Task },
                new { Title = "second", Number = 2, Type = Domain.Tasks.TaskType.Task },
            ],
            tasks);
    }

    private static AppDbContext SqliteContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection, sqlite => sqlite.MigrationsAssembly(ZaneTask.Infrastructure.DependencyInjection.SqliteMigrationsAssembly))
            .Options);

    /// <summary>Guids in the same TEXT format EF Core uses for SQLite.</summary>
    private static string NewId() => Guid.NewGuid().ToString().ToUpperInvariant();

    private static void Exec(SqliteConnection connection, string sql, params (string Name, string Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task Sqlite_reads_timestamps_back_as_utc()
    {
        using var t = new TestDatabase();
        var alice = t.AddUser("Alice");
        t.ActAs(alice);
        var created = await t.Projects.CreateAsync(new("P", null), default);

        await using var fresh = t.NewContext();
        var project = await fresh.Projects.SingleAsync(p => p.Id == created.Id);
        Assert.Equal(DateTimeKind.Utc, project.CreatedAt.Kind);
    }
}
