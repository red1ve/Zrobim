using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Zrobim.Api.Auth;

/// <summary>JWT settings: section "Jwt" in appsettings, the signing key comes from .env (Jwt__SigningKey).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    /// <summary>HMAC-SHA256 secret, at least 32 bytes. Never committed — lives in .env / environment.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; init; } = null!;

    /// <summary>
    /// Access token lifetime. There are no refresh tokens in the MVP, so it is long enough for a
    /// mobile app; a blocked user loses access immediately because every request re-checks is_active.
    /// </summary>
    [Range(1, 24 * 30)]
    public int TokenLifetimeHours { get; init; }

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
