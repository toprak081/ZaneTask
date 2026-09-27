using ZaneTask.Contracts;

namespace ZaneTask.Application.Abstractions;

/// <summary>Tells everyone viewing a project that its board changed. Implementations must not throw.</summary>
public interface IBoardNotifier
{
    Task NotifyAsync(BoardEvent boardEvent, CancellationToken cancellationToken);
}
