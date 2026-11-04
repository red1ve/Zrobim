using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Zrobim.Api.Users;

namespace Zrobim.Api.Auth;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider time)
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken CreateToken(User user)
    {
        var jwt = options.Value;
        var now = time.GetUtcNow();
        var expiresAt = now.AddHours(jwt.TokenLifetimeHours);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [AuthClaims.Role] = Roles.Of(user.Role),
            },
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(Handler.CreateToken(descriptor), expiresAt);
    }
}
