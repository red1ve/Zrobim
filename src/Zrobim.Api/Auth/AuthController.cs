using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zrobim.Api.Data;
using Zrobim.Api.Masters;
using Zrobim.Api.Users;

namespace Zrobim.Api.Auth;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(AuthServiceCollectionExtensions.RateLimitPolicy)]
public class AuthController(ZrobimDbContext db, IPasswordHasher<User> passwordHasher, JwtTokenService tokens)
    : ControllerBase
{
    // Verified against when the email is unknown, so that a wrong email and a wrong password
    // take the same time and an attacker cannot find out which emails are registered.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(new User(), "not-a-real-password");

    /// <summary>Registers a client or a master and signs them in.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        if (request.Role is not (UserRole.Client or UserRole.Master))
        {
            ModelState.AddModelError(nameof(request.Role), "Зарегистрироваться можно только как CLIENT или MASTER.");
            return ValidationProblem(ModelState);
        }

        var user = new User
        {
            Email = NormalizeEmail(request.Email),
            Phone = NullIfBlank(request.Phone),
            FirstName = request.FirstName.Trim(),
            LastName = NullIfBlank(request.LastName),
            Role = request.Role.Value,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);

        // A master gets a profile right away; it stays PENDING until an admin approves it.
        Master? master = null;
        if (user.Role == UserRole.Master)
            db.Masters.Add(master = new Master { User = user });

        try
        {
            // one SaveChanges = one transaction: the user and the master profile are created together
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
                                          {
                                              SqlState: PostgresErrorCodes.UniqueViolation,
                                              ConstraintName: "users_email_lower_key" or "users_phone_key",
                                          } unique)
        {
            // the unique indexes, not a prior SELECT, decide — so two parallel registrations cannot both win
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: unique.ConstraintName == "users_phone_key"
                    ? "Этот номер телефона уже зарегистрирован."
                    : "Этот email уже зарегистрирован.");
        }

        return Created("/api/me", CreateAuthResponse(user, master?.Id));
    }

    /// <summary>Signs in with email and password.</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        // lower(email) matches the users_email_lower_key index
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(new User(), DummyPasswordHash, request.Password);
            return InvalidCredentials();
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return InvalidCredentials();

        if (!user.IsActive)
            return Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Аккаунт заблокирован. Обратитесь в поддержку.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        var masterId = user.Role == UserRole.Master
            ? await db.Masters.Where(m => m.UserId == user.Id).Select(m => (Guid?)m.Id).SingleOrDefaultAsync(ct)
            : null;

        return Ok(CreateAuthResponse(user, masterId));
    }

    private AuthResponse CreateAuthResponse(User user, Guid? masterId)
    {
        var token = tokens.CreateToken(user);
        return new AuthResponse(token.Token, token.ExpiresAt, UserResponse.From(user, masterId));
    }

    private ObjectResult InvalidCredentials() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Неверный email или пароль.");

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
