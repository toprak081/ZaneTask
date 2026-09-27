namespace ZaneTask.Contracts;

/// <summary>What changed on a board; clients reload the matching data.</summary>
public enum BoardChange
{
    /// <summary>Tasks were created, edited, moved, (un)labelled or deleted.</summary>
    Tasks = 0,

    /// <summary>Project details, members or labels changed (tasks may be affected too).</summary>
    Project = 1,

    /// <summary>Comments of <see cref="BoardEvent.TaskId"/> changed.</summary>
    Comments = 2,

    ProjectDeleted = 3,
}

/// <summary>
/// Pushed to everyone viewing a project. Carries ids only — clients fetch the data through the API,
/// so the usual authorization still applies.
/// </summary>
public sealed record BoardEvent(Guid ProjectId, BoardChange Change, Guid? TaskId, Guid ActorId);

/// <summary>Names shared by the SignalR hub and its clients.</summary>
public static class BoardHubContract
{
    public const string Path = "/hubs/board";

    // Client -> server
    public const string JoinProject = "JoinProject";
    public const string LeaveProject = "LeaveProject";

    // Server -> client
    public const string BoardChanged = "BoardChanged";
}
