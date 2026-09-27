using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;

namespace ZaneTask.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) =>
        auth.RegisterAsync(request, ct);

    [AllowAnonymous]
    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) =>
        auth.LoginAsync(request, ct);

    [HttpGet("me")]
    public async Task<UserDto> Me(ICurrentUser currentUser, IUserDirectory users, CancellationToken ct)
    {
        var directory = await users.GetByIdsAsync([currentUser.UserId], ct);
        return directory.TryGetValue(currentUser.UserId, out var user)
            ? user
            : throw new NotFoundException("User", currentUser.UserId);
    }
}
