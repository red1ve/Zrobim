namespace Zrobim.Api.Users;

/// <summary>The current user as the mobile app sees it. <c>MasterId</c> is set only for masters.</summary>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string? Phone,
    string FirstName,
    string? LastName,
    string? AvatarUrl,
    UserRole Role,
    Guid? MasterId)
{
    public static UserResponse From(User user, Guid? masterId) =>
        new(user.Id, user.Email, user.Phone, user.FirstName, user.LastName, user.AvatarUrl, user.Role, masterId);
}
