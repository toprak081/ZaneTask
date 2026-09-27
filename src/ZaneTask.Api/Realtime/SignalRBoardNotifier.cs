using Microsoft.AspNetCore.SignalR;
using ZaneTask.Application.Abstractions;
using ZaneTask.Contracts;

namespace ZaneTask.Api.Realtime;

internal sealed class SignalRBoardNotifier(IHubContext<BoardHub> hub, ILogger<SignalRBoardNotifier> logger) : IBoardNotifier
{
    public async Task NotifyAsync(BoardEvent boardEvent, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients
                .Group(BoardHub.GroupName(boardEvent.ProjectId))
                .SendAsync(BoardHubContract.BoardChanged, boardEvent, cancellationToken);
        }
        catch (Exception e)
        {
            // The change is already saved; a missed live update only means viewers see it on their next refresh.
            logger.LogWarning(e, "Could not broadcast {Change} for project {ProjectId}", boardEvent.Change, boardEvent.ProjectId);
        }
    }
}
