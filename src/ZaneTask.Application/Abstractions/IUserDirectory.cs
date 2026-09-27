using ZaneTask.Contracts;

namespace ZaneTask.Application.Abstractions;

/// <summary>Read access to user accounts, which live outside the domain model.</summary>
public interface IUserDirectory
{
    Task<UserDto?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Returns the users that exist among <paramref name="ids"/>, keyed by id.</summary>
    Task<IReadOnlyDictionary<Guid, UserDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
}
