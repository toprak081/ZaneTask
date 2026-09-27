using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ZaneTask.Application.Abstractions;
using ZaneTask.Application.Common;
using ZaneTask.Contracts;

namespace ZaneTask.Infrastructure.Identity;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock) : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var displayName = request.DisplayName.Trim();
        if (displayName.Length is 0 or > ApplicationUser.DisplayNameMaxLength)
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                [nameof(request.DisplayName)] = [$"Display name must be 1-{ApplicationUser.DisplayNameMaxLength} characters."],
            });
        }

        if (await userManager.FindByEmailAsync(request.Email) is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = new ApplicationUser
        {
            Email = request.Email.Trim(),
            UserName = request.Email.Trim(),
            DisplayName = displayName,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code.Contains("Password", StringComparison.Ordinal) ? nameof(request.Password) : nameof(request.Email))
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            throw new ValidationFailedException(errors);
        }

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new AuthenticationFailedException(InvalidCredentials);

        if (await userManager.IsLockedOutAsync(user))
            throw new AuthenticationFailedException("Account is temporarily locked. Try again later.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(ApplicationUser user)
    {
        var options = jwtOptions.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(options.AccessTokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AuthResponse(token, expires, new UserDto(user.Id, user.Email!, user.DisplayName));
    }
}
