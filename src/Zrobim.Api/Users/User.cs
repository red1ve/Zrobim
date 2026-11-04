namespace Zrobim.Api.Users;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum UserRole
{
    Client,
    Master,
    Admin,
}
