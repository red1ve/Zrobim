using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Zrobim.Api.Auth;

/// <summary>Claim names used in our tokens (kept short and unmapped: "sub", "role").</summary>
public static class AuthClaims
{
    public const string UserId = JwtRegisteredClaimNames.Sub;
    public const string Role = "role";

    /// <summary>Id of the authenticated user. Only call on endpoints protected by [Authorize].</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(UserId)
            ?? throw new InvalidOperationException("The principal has no 'sub' claim."));
}
