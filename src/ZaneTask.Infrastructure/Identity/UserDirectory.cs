using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZaneTask.Application.Abstractions;
using ZaneTask.Contracts;
using ZaneTask.Infrastructure.Persistence;

namespace ZaneTask.Infrastructure.Identity;

internal sealed class UserDirectory(AppDbContext db, ILookupNormalizer normalizer) : IUserDirectory
{
    public async Task<UserDto?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = normalizer.NormalizeEmail(email.Trim());
        return await db.Users
            .AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized)
            .Select(u => new UserDto(u.Id, u.Email!, u.DisplayName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, UserDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, UserDto>();

        return await db.Users
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .Select(u => new UserDto(u.Id, u.Email!, u.DisplayName))
            .ToDictionaryAsync(u => u.Id, ct);
    }
}
