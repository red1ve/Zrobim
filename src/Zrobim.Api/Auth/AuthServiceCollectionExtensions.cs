using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Zrobim.Api.Data;
using Zrobim.Api.Users;

namespace Zrobim.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>Rate-limit policy for login/registration: 10 requests per minute per IP.</summary>
    public const string RateLimitPolicy = "auth";

    public static IServiceCollection AddZrobimAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<JwtTokenService>();
        // PBKDF2 (HMAC-SHA512, 100 000 iterations) from ASP.NET Core Identity
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;  // keep "sub" and "role" as they are in the token
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = AuthClaims.UserId,
                    RoleClaimType = AuthClaims.Role,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectInactiveUsers };
            });
        services.AddAuthorization();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    /// <summary>
    /// A token stays valid until it expires, so blocking is checked on every request:
    /// a blocked (is_active = false) or deleted user is rejected immediately.
    /// </summary>
    private static async Task RejectInactiveUsers(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(AuthClaims.UserId), out var userId))
        {
            context.Fail("The token has no valid user id.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ZrobimDbContext>();
        var isActive = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.IsActive)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (isActive != true)
            context.Fail("The user is blocked or no longer exists.");
    }
}
