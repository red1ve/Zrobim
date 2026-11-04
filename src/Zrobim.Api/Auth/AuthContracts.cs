using System.ComponentModel.DataAnnotations;
using Zrobim.Api.Users;

namespace Zrobim.Api.Auth;

public sealed record RegisterRequest(
    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    [MaxLength(255, ErrorMessage = "Email не длиннее 255 символов.")]
    string Email,

    [Required(ErrorMessage = "Укажите пароль.")]
    [MinLength(8, ErrorMessage = "Пароль не короче 8 символов.")]
    [MaxLength(128, ErrorMessage = "Пароль не длиннее 128 символов.")]
    string Password,

    [Required(ErrorMessage = "Укажите имя.")]
    [MaxLength(100, ErrorMessage = "Имя не длиннее 100 символов.")]
    string FirstName,

    [MaxLength(100, ErrorMessage = "Фамилия не длиннее 100 символов.")]
    string? LastName,

    [Phone(ErrorMessage = "Некорректный номер телефона.")]
    [MaxLength(30, ErrorMessage = "Телефон не длиннее 30 символов.")]
    string? Phone,

    // only CLIENT or MASTER; admins are never self-registered
    [Required(ErrorMessage = "Укажите роль: CLIENT или MASTER.")]
    UserRole? Role);

public sealed record LoginRequest(
    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Некорректный email.")]
    [MaxLength(255, ErrorMessage = "Email не длиннее 255 символов.")]
    string Email,

    [Required(ErrorMessage = "Укажите пароль.")]
    [MaxLength(128, ErrorMessage = "Пароль не длиннее 128 символов.")]
    string Password);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
