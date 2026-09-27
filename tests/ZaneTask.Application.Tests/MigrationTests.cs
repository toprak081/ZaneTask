using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
