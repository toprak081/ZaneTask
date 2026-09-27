using System.ComponentModel.DataAnnotations;

namespace ZaneTask.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    /// <summary>HMAC-SHA256 key; at least 32 characters. Keep it out of source control outside development.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    /// <summary>Up to 30 days; the desktop app uses the maximum so you stay signed in.</summary>
    [Range(1, 30 * 24 * 60)]
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
