using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Projects;
using ZaneTask.Application.Tasks;
using ZaneTask.Infrastructure.Identity;
using ZaneTask.Infrastructure.Persistence;

namespace ZaneTask.Application.Tests;

/// <summary>A fresh in-memory SQLite database per test, with the application services wired to it.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FakeCurrentUser _currentUser = new();

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Db = NewContext();
        Db.Database.EnsureCreated();

        var users = new UserDirectory(Db, new UpperInvariantLookupNormalizer());
        var clock = TimeProvider.System;
        Projects = new ProjectService(Db, _currentUser, users, clock);
        Tasks = new TaskService(Db, _currentUser, users, clock);
        Comments = new CommentService(Db, _currentUser, users, clock);
    }

    public AppDbContext Db { get; }
    public ProjectService Projects { get; }
    public TaskService Tasks { get; }
    public CommentService Comments { get; }

    /// <summary>A separate context on the same database, for checking what was actually persisted.</summary>
    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public Guid AddUser(string displayName)
    {
        var email = $"{displayName.ToLowerInvariant()}@example.com";
        var user = new ApplicationUser
        {
            DisplayName = displayName,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
        };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user.Id;
    }

    /// <summary>Makes subsequent service calls run as <paramref name="userId"/>.</summary>
    public void ActAs(Guid userId)
    {
        _currentUser.UserId = userId;
        Db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId { get; set; }
    }
}
