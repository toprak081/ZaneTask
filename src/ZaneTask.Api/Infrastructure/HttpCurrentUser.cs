using ZaneTask.Application.Abstractions;

namespace ZaneTask.Api.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirst("sub")?.Value;
            return Guid.TryParse(sub, out var id)
                ? id
                : throw new InvalidOperationException("No authenticated user on the current request.");
        }
    }
}
