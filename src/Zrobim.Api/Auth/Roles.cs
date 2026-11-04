using Zrobim.Api.Users;

namespace Zrobim.Api.Auth;

/// <summary>Role names as they appear in the JWT "role" claim and in [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Client = "CLIENT";
    public const string Master = "MASTER";
    public const string Admin = "ADMIN";

    public static string Of(UserRole role) => role switch
    {
        UserRole.Client => Client,
        UserRole.Master => Master,
        UserRole.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
