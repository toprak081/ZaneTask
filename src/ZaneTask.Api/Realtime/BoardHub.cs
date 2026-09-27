using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Contracts;

namespace ZaneTask.Api.Realtime;

/// <summary>
/// Clients join the group of the project they are viewing and receive <see cref="BoardEvent"/>s for it.
/// Events carry ids only; data is always fetched through the (authorized) REST API.
/// </summary>
[Authorize]
public sealed class BoardHub(IAppDbContext db) : Hub
{
    public static string GroupName(Guid projectId) => $"project:{projectId}";

    [HubMethodName(BoardHubContract.JoinProject)]
    public async Task JoinProject(Guid projectId)
    {
        if (!Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var userId) ||
            !await db.Projects.AnyAsync(p => p.Id == projectId && p.Members.Any(m => m.UserId == userId)))
        {
            // Same answer for "doesn't exist" and "not a member", like the REST API's 404.
            throw new HubException("Project not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));
    }

    [HubMethodName(BoardHubContract.LeaveProject)]
    public Task LeaveProject(Guid projectId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));
}
